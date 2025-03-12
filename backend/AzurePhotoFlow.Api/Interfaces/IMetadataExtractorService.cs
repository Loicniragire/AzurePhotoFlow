using AzurePhotoFlow.POCO.QueueModels;

namespace Api.Interfaces;

public interface IMetadataExtractorService
{
    CameraGeneratedMetadata GetCameraGeneratedMetadata(Stream image);
}
