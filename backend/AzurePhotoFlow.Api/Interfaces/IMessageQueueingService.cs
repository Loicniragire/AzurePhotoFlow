
namespace Api.Interfaces;
public interface IMessageQueueingService
{
	Task EnqueueMessageAsync(string message);
}
