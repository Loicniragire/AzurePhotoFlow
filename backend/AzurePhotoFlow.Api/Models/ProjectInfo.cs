using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Models;
public class ProjectInfo
{
    [Required]
    [JsonPropertyName("ProjectName")]
    public string Name { get; set; }

    [Required]
    [JsonPropertyName("TimeStamp")]
    public DateTime Datestamp { get; set; }

    [JsonPropertyName("Directories")]
    public List<ProjectDirectory> Directories { get; set; } = new List<ProjectDirectory>();
}

public class ProjectDirectory
{
    [Required]
    [JsonPropertyName("DirectoryName")]
    public string Name { get; set; }

    [JsonPropertyName("RawFilesCount")]
    public int RawFilesCount { get; set; }

    [JsonPropertyName("ProcessedFilesCount")]
    public int ProcessedFilesCount { get; set; }
}

