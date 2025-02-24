using Api.Interfaces;
using Azure.Storage.Queues;

namespace AzurePhotoFlow.Services;

public class MessageQueueingService : IMessageQueueingService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<MessageQueueingService> _logger;

    public MessageQueueingService(QueueServiceClient queueServiceClient,
                                       string queueName,
                                       ILogger<MessageQueueingService> logger)
    {
        if (queueServiceClient == null)
            throw new ArgumentNullException(nameof(queueServiceClient));
        _logger = logger;

        _queueClient = queueServiceClient.GetQueueClient(queueName);
		_logger.LogInformation($"Creating queue if it does not exist. QueueName: {queueName}");
        _queueClient.CreateIfNotExists();
        _logger.LogInformation($"Using queue: QueueName: {queueName}");
    }

    /// <summary>
    /// Enqueues a message to the Azure Storage Queue.
    /// </summary>
    /// <param name="message">The message to be enqueued.</param>
    public async Task EnqueueMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));

        await _queueClient.SendMessageAsync(message);
    }
}
