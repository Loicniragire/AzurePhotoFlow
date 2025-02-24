using Functions.Models;
using Microsoft.Extensions.Logging;

namespace Functions.Interfaces;
public interface IMetadataProcessor
{
	Task ProcessAsync(ImageMetadata metadata, ILogger log);
}
