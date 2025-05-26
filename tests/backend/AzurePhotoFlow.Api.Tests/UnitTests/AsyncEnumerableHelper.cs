
namespace unitTests;
public static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            // Yielding ensures that the enumeration is deferred.
            yield return item;
        }
        // The await Task.CompletedTask is not strictly necessary for yield return to work
        // but can be useful if you needed an async method signature for other reasons.
        // For this specific conversion, it's often omitted or can be a simple await Task.Yield();
        await Task.CompletedTask; 
    }
}

