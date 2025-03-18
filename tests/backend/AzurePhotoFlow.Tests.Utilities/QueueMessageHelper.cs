using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace PhotoFlow.Utilities;

public class QueueMessageHelper
{
    private const string DefaultAccountName = "devstoreaccount1";
    private const string DefaultAccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private const string DefaultQueueEndpoint = "http://127.0.0.1:10001/devstoreaccount1";
    private const string DefaultQueueName = "image-metadata-queue";

    private readonly QueueClient _queueClient;

    public QueueMessageHelper(string queueName = DefaultQueueName)
    {
        try
        {
            var connectionString = $"DefaultEndpointsProtocol=http;AccountName={DefaultAccountName};AccountKey={DefaultAccountKey};QueueEndpoint={DefaultQueueEndpoint}";

            _queueClient = new QueueClient(connectionString, queueName);
            _queueClient.CreateIfNotExists();

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            _queueClient = null;

        }
    }

    public void AddMessage(string message)
    {
        _queueClient.SendMessage(message);
    }

    public string[] ReadMessage()
    {
        QueueMessage[] messages = _queueClient.ReceiveMessages();
        return messages.Select(m => m.MessageText).ToArray();
    }
}

