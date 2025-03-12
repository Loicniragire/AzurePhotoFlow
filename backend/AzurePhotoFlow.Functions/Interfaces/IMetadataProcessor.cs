using AzurePhotoFlow.POCO.QueueModels;

namespace Functions.Interfaces;
public interface IMetadataProcessor
{
	Task ProcessAsync(ImageMetadata metadata);
}
