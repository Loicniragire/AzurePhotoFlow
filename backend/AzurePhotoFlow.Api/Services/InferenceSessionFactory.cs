using Microsoft.ML.OnnxRuntime;

namespace AzurePhotoFlow.Services;

public static class InferenceSessionFactory
{
    public static InferenceSession Create()
    {
        var modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH");
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            throw new InvalidOperationException("Inference service is not configured. Set CLIP_MODEL_PATH to a valid ONNX model path.");
        }
        return new InferenceSession(modelPath);
    }
}
