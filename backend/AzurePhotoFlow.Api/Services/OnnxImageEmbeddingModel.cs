using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Security.Cryptography;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace AzurePhotoFlow.Services;

public class OnnxImageEmbeddingModel : IImageEmbeddingModel
{
    private readonly InferenceSession _session;

    public OnnxImageEmbeddingModel(InferenceSession session)
    {
        _session = session;
    }

    public float[] GenerateEmbedding(byte[] imageBytes)
    {
        using var image = SixLaborsImage.Load<Rgb24>(imageBytes);
        image.Mutate(x => x.Resize(224, 224));
        var tensor = new DenseTensor<float>(new[] { 1, 3, 224, 224 });
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 224; x++)
            {
                var p = image[x, y];
                tensor[0, 0, y, x] = p.R / 255f;
                tensor[0, 1, y, x] = p.G / 255f;
                tensor[0, 2, y, x] = p.B / 255f;
            }
        }
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", tensor) };
        using var results = _session.Run(inputs);
        return results.First().AsEnumerable<float>().ToArray();
    }

    public float[] GenerateTextEmbedding(string text)
    {
        // TODO: This is a simplified implementation for demonstration.
        // In a production environment, you would need:
        // 1. A proper CLIP text encoder ONNX model
        // 2. Text tokenization using CLIP's tokenizer
        // 3. Proper text preprocessing and encoding
        
        // For now, create a deterministic embedding based on text content
        // This allows basic functionality while maintaining consistency
        var embedding = CreateDeterministicEmbedding(text);
        
        // Normalize the embedding to unit length (similar to CLIP outputs)
        return NormalizeVector(embedding);
    }

    private float[] CreateDeterministicEmbedding(string text)
    {
        // Create a 512-dimensional embedding based on text hash
        var embedding = new float[512];
        
        // Use text content to generate deterministic values
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant().Trim()));
        
        // Convert hash bytes to float values
        for (int i = 0; i < 512; i++)
        {
            var byteIndex = i % hash.Length;
            var wordIndex = (i / hash.Length) % text.Length;
            
            // Combine hash byte with character from text to create variation
            var combined = hash[byteIndex] + (text[wordIndex] * 7);
            embedding[i] = (float)Math.Sin(combined * 0.01) * 0.5f;
        }
        
        // Add some semantic similarity for common words
        AddSemanticSimilarity(text, embedding);
        
        return embedding;
    }

    private void AddSemanticSimilarity(string text, float[] embedding)
    {
        // Simple semantic grouping for common photo-related terms
        var semanticGroups = new Dictionary<string[], float[]>
        {
            { new[] { "dog", "dogs", "puppy", "puppies", "pet", "pets" }, new float[] { 0.8f, 0.2f, 0.5f, 0.9f } },
            { new[] { "cat", "cats", "kitten", "kittens", "feline" }, new float[] { 0.7f, 0.3f, 0.6f, 0.8f } },
            { new[] { "tree", "trees", "forest", "woods", "nature" }, new float[] { 0.1f, 0.9f, 0.2f, 0.7f } },
            { new[] { "water", "ocean", "sea", "lake", "river", "beach" }, new float[] { 0.2f, 0.1f, 0.8f, 0.6f } },
            { new[] { "car", "cars", "vehicle", "automobile", "truck" }, new float[] { 0.9f, 0.1f, 0.3f, 0.4f } },
            { new[] { "person", "people", "human", "man", "woman", "child" }, new float[] { 0.5f, 0.7f, 0.9f, 0.3f } }
        };

        var lowerText = text.ToLowerInvariant();
        
        foreach (var group in semanticGroups)
        {
            if (group.Key.Any(keyword => lowerText.Contains(keyword)))
            {
                // Blend semantic features into specific positions of the embedding
                for (int i = 0; i < group.Value.Length && i < 50; i++)
                {
                    embedding[i * 10] += group.Value[i] * 0.3f; // Add semantic signal
                }
                break;
            }
        }
    }

    private float[] NormalizeVector(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] = (float)(vector[i] / magnitude);
            }
        }
        return vector;
    }
}
