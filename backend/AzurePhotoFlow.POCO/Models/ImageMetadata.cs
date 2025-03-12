using Newtonsoft.Json;

namespace AzurePhotoFlow.POCO.Models;

public class ImageMetadata
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("blobUri")]
    public string BlobUri { get; set; }

    [JsonProperty("uploadedBy")]
    public string UploadedBy { get; set; }

    [JsonProperty("tags")]
    public List<string> Tags { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("uploadDate")]
    public DateTimeOffset UploadDate { get; set; }

	[JsonProperty("cameraGeneratedMetadata")]
	public CameraGeneratedMetadata CameraGeneratedMetadata { get; set; }
}


