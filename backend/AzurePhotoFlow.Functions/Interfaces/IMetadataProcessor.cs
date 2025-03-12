using AzurePhotoFlow.POCO.Models;

namespace Functions.Interfaces;
public interface IMetadataProcessor
{
	Task ProcessAsync(ImageMetadata metadata);
}
