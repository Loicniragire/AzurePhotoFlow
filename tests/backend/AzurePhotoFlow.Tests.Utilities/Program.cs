using PhotoFlow.Utilities;

try
{

	AzuriteQueueServiceHelper.EnsureAzuriteQueueServiceIsRunningAsync().Wait();
	Console.WriteLine("Azurite queue service is running.");

	var queueMessageHelper = new QueueMessageHelper();
	Console.WriteLine("Queue operation: post or read?");
	var action = Console.ReadLine();
	if (action == "post")
	{
		Console.WriteLine("Enter the message you want to post:");
		var message = Console.ReadLine();
		queueMessageHelper.AddMessage(message);
	}
	else if (action == "read")
	{
		var messages = queueMessageHelper.ReadMessage();
		foreach (var message in messages)
		{
			Console.WriteLine(message);
		}
	}
	else
	{
		Console.WriteLine("Invalid action");
	}
}
catch (Exception ex)
{
	Console.WriteLine(ex.Message);
}
