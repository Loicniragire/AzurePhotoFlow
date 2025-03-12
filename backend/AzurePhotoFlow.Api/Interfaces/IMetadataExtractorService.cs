using AzurePhotoFlow.POCO.Models;
namespace Api.Interfaces;

public interface IMetadataExtractorService
{
    CameraGeneratedMetadata GetCameraGeneratedMetadata(Stream image);
}
