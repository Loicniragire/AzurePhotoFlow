namespace Api.Interfaces;

public interface IEmbeddingNotificationService
{
    Task NotifyAsync(string projectName, string directoryName, DateTime timestamp);
}
