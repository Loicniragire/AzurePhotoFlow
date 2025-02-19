using Api.Models;
namespace Api.Interfaces;

public interface IMetadataExtractorService
{
    CameraGeneratedMetadata GetCameraGeneratedMetadata(Stream image);
}
