using Microsoft.ML.OnnxRuntime;
using System.Linq;

namespace AzurePhotoFlow.Services;

public class OnnxSession : IOnnxSession
{
    private readonly InferenceSession _session;
    public OnnxSession(InferenceSession session)
    {
        _session = session;
    }

    public IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(IEnumerable<NamedOnnxValue> inputs)
    {
        return _session.Run(inputs.ToList());
    }
}
