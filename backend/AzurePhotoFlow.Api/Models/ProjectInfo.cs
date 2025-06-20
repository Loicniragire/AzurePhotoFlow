using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Models;
public class ProjectInfo
{
    [Required]
    [JsonPropertyName("projectName")]
    public string Name { get; set; }

    [Required]
    [JsonPropertyName("timestamp")]
    public DateTime Datestamp { get; set; }

    [JsonPropertyName("directories")]
    public List<ProjectDirectory> Directories { get; set; } = new List<ProjectDirectory>();
}

public class ProjectDirectory
{
    [Required]
    [JsonPropertyName("directoryName")]
    public string Name { get; set; }

    [JsonPropertyName("rawFilesCount")]
    public int RawFilesCount { get; set; }

    [JsonPropertyName("processedFilesCount")]
    public int ProcessedFilesCount { get; set; }
}

