using Microsoft.ML.OnnxRuntime;

namespace AzurePhotoFlow.Services;

public interface IOnnxSession
{
    IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(IEnumerable<NamedOnnxValue> inputs);
}
