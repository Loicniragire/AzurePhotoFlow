using MetadataExtractor;
using MetadataDirectory = MetadataExtractor.Directory;

namespace AzurePhotoFlow.Services;

/// Helper class to safely convert values without throwing exceptions.
public static class MetadataHelper
{
    public static Rational? SafeGetRational(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetRational(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    public static int? SafeGetInt32(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetInt32(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? SafeGetDateTime(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetDateTime(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    public static string SafeGetDescription(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetDescription(tag) : null;
        }
        catch
        {
            return null;
        }
    }

    public static string SafeGetString(MetadataDirectory directory, int tag)
    {
        try
        {
            return directory.ContainsTag(tag) ? directory.GetString(tag) : null;
        }
        catch
        {
            return null;
        }
    }
}

