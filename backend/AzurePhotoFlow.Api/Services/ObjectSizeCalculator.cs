using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzurePhotoFlow.Services;

public static class ObjectSizeCalculator
{
    public static double GetSerializedSizeInKB<T>(T instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        var options = new JsonSerializerOptions
        {
            Converters = { new RationalConverter() },
            ReferenceHandler = ReferenceHandler.Preserve,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Increase the maximum depth to accommodate deeper object graphs.
            MaxDepth = 256
        };

        // Serialize the instance using the configured options.
        var json = JsonSerializer.Serialize(instance, options);
        var byteCount = Encoding.UTF8.GetByteCount(json);
        return byteCount / 1024.0;
    }
}

