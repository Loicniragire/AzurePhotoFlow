using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Minio.DataModel.Args;

namespace AzurePhotoFlow.Services;

public static class MinIODirectoryHelper
{

    public static bool IsDirectDescendant(ZipArchiveEntry entry, string parentDir) =>
        entry.FullName.StartsWith(parentDir + "/", StringComparison.OrdinalIgnoreCase) &&
        entry.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2;

    public static bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp" or ".gif";
    }

    public static string GetMimeType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".tif" or ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }


    /// <summary>
    /// Removes the first path segment (<paramref name="parentDirectory"/>) from
    /// <paramref name="entryFullName"/> and returns the remainder.
    ///
    /// Examples
    /// ─────────────────────────────────────────────────────────────
    ///   parentDirectory = "CameraA"
    ///   entryFullName   = "CameraA/IMG_0001.jpg"      →  "IMG_0001.jpg"
    ///
    ///   parentDirectory = "CameraA"
    ///   entryFullName   = "CameraA/Sub/IMG_0002.jpg"  →  "Sub/IMG_0002.jpg"
    ///
    ///   parentDirectory = "CameraA"
    ///   entryFullName   = "Other/IMG_0003.jpg"        →  "Other/IMG_0003.jpg"  (no match)
    /// </summary>
    public static string GetRelativePath(string entryFullName, string parentDirectory)
    {
        if (string.IsNullOrEmpty(entryFullName) || string.IsNullOrEmpty(parentDirectory))
            return entryFullName;

        // ZipArchiveEntry.FullName always uses forward slashes, independent of OS.
        string prefix = parentDirectory.TrimEnd('/') + '/';

        return entryFullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               ? entryFullName[prefix.Length..]      // slice off the prefix
               : entryFullName;                      // parentDirectory not present – return unchanged
    }

    public static string GetDestinationPath(
        DateTime timestamp,
        string projectName,
        string directoryName,
        bool isRawFiles)
    {
        // 1)  yyyy-MM-dd   → keeps objects in date partitions for easier lifecycle rules
        // 2)  project name → one level per customer / shoot / collection
        // 3)  category     → RawFiles | ProcessedFiles
        // 4)  directory    → typically the folder inside the ZIP
        //
        // Result example:
        //   2025/05/13/WeddingSmith/RawFiles/CameraA
        //   2025/05/13/WeddingSmith/ProcessedFiles/CameraA

        string datePart = timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string category = isRawFiles ? "RawFiles" : "ProcessedFiles";

        return $"{datePart}/{Sanitize(projectName)}/{category}/{Sanitize(directoryName)}";
    }

    /// <summary>
    /// Removes or normalises characters that are illegal or awkward in S‑3 keys,
    /// Azure blob names, or Windows paths (`\`, `..`, leading `/`, etc.).
    /// </summary>
    public static string Sanitize(string value)
    {
        return value
            .Trim()
            .Replace('\\', '/')
            .Replace("..", string.Empty)
            .Trim('/')
            .Replace("  ", " ");   // collapse double spaces, optional
    }

    public static bool IsValidBucketName(string name)
    {
        const string pattern =
            @"^(?!xn--)(?!sthree-)(?!amzn-s3-demo-)(?!.*\.(?:mrap)$)(?!.*--(?:ol|x|table)-s3$)(?!\d{1,3}(?:\.\d{1,3}){3}$)[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])?$";

        return Regex.IsMatch(name, pattern, RegexOptions.Compiled);
    }

}


