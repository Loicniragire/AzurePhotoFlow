namespace Api.Interfaces;

public interface IEmbeddingService
{
    Task GenerateAsync(string projectName, string directoryName, DateTime timestamp);
}
