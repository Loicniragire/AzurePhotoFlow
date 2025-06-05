namespace Api.Interfaces;

public interface IEmbeddingGeneratorService
{
    Task GenerateAsync(string projectName, string directoryName, DateTime timestamp);
}
