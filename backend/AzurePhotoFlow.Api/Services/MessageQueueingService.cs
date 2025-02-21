using Api.Interfaces;
using Azure.Storage.Queues;

namespace AzurePhotoFlow.Services;
public class MessageQueueingService : IMessageQueueingService
{
    private readonly QueueClient _queueClient;

    /// <summary>
    /// Initializes the service and ensures the queue exists.
    /// </summary>
    /// <param name="connectionString">The Azure Storage connection string.</param>
    /// <param name="queueName">The name of the queue.</param>
    public MessageQueueingService(string connectionString, string queueName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));

        _queueClient = new QueueClient(connectionString, queueName);
        // Create the queue if it does not exist.
        _queueClient.CreateIfNotExists();
    }

    /// <summary>
    /// Enqueues a message to the Azure Storage Queue.
    /// </summary>
    /// <param name="message">The message to be enqueued.</param>
    public async Task EnqueueMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));

        // Send the message to the queue.
        await _queueClient.SendMessageAsync(message);
    }
}

