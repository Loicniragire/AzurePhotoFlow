using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace AzurePhotoFlow.Services;

public class OnnxImageEmbeddingModel : IImageEmbeddingModel
{
    private readonly InferenceSession _visionSession;
    private readonly InferenceSession? _textSession;
    private readonly Dictionary<string, object>? _tokenizer;
    private readonly ILogger<OnnxImageEmbeddingModel> _logger;
    private const int InputSize = 224;
    private const int EmbeddingSize = 38400; // 50 * 768
    private const int MaxTokenLength = 77;

    public OnnxImageEmbeddingModel(InferenceSession visionSession, InferenceSession? textSession = null, Dictionary<string, object>? tokenizer = null, ILogger<OnnxImageEmbeddingModel>? logger = null)
    {
        _visionSession = visionSession;
        _textSession = textSession;
        _tokenizer = tokenizer;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OnnxImageEmbeddingModel>.Instance;
        
        // Debug log which models are available
        _logger.LogInformation("[EMBEDDING DEBUG] OnnxImageEmbeddingModel initialized:");
        _logger.LogInformation("[EMBEDDING DEBUG] - Vision model: Available (for image embeddings)");
        _logger.LogInformation("[EMBEDDING DEBUG] - Text model: {TextModelStatus} (for text embeddings)", 
            textSession != null ? "Available (CLIP text model)" : "NOT AVAILABLE (will use fallback)");
        _logger.LogInformation("[EMBEDDING DEBUG] - Tokenizer: {TokenizerStatus}", 
            tokenizer != null ? "Available" : "NOT AVAILABLE");
    }

    public async Task<float[]> GenerateImageEmbedding(Stream imageStream)
    {
        _logger.LogInformation("[EMBEDDING DEBUG] Using vision model for image embedding generation");
        using var image = await SixLaborsImage.LoadAsync<Rgb24>(imageStream);
        image.Mutate(x => x.Resize(InputSize, InputSize));

        var pixelData = new float[1 * 3 * InputSize * InputSize];
        var pixelIndex = 0;

        // Convert to RGB float array in CHW format (channels first)
        for (int c = 0; c < 3; c++) // RGB channels
        {
            for (int y = 0; y < InputSize; y++)
            {
                for (int x = 0; x < InputSize; x++)
                {
                    var pixel = image[x, y];
                    float value = c switch
                    {
                        0 => pixel.R / 255.0f, // Red
                        1 => pixel.G / 255.0f, // Green
                        2 => pixel.B / 255.0f, // Blue
                        _ => 0
                    };
                    
                    // Normalize using ImageNet statistics
                    value = c switch
                    {
                        0 => (value - 0.485f) / 0.229f, // Red normalization
                        1 => (value - 0.456f) / 0.224f, // Green normalization
                        2 => (value - 0.406f) / 0.225f, // Blue normalization
                        _ => value
                    };
                    
                    pixelData[pixelIndex++] = value;
                }
            }
        }

        var inputTensor = new DenseTensor<float>(pixelData, new[] { 1, 3, InputSize, InputSize });
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };
        
        using var results = _visionSession.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();
        
        // Flatten the output to get the final embedding
        return output.Take(EmbeddingSize).ToArray();
    }

    public float[] GenerateTextEmbedding(string text)
    {
        // If no text model available, fall back to placeholder
        if (_textSession == null || _tokenizer == null)
        {
            _logger.LogInformation("[EMBEDDING DEBUG] No text model available - using fallback hash-based text embedding for: '{Text}'", text);
            return GenerateTextEmbeddingFromString(text);
        }

        try
        {
            _logger.LogInformation("[EMBEDDING DEBUG] Using CLIP text model for text embedding generation: '{Text}'", text);
            return GenerateRealTextEmbedding(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[EMBEDDING DEBUG] CLIP text model failed: {ErrorMessage} - falling back to hash-based embedding for: '{Text}'", ex.Message, text);
            // Fall back to placeholder on error
            return GenerateTextEmbeddingFromString(text);
        }
    }

    public float[] GenerateEmbedding(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        return GenerateImageEmbedding(stream).Result;
    }

    private float[] GenerateRealTextEmbedding(string text)
    {
        // Tokenize the text
        var tokens = TokenizeText(text);
        
        // Create input tensors
        var inputIds = new long[MaxTokenLength];
        var attentionMask = new long[MaxTokenLength];
        
        // Fill with tokens (pad with 0s if shorter, truncate if longer)
        for (int i = 0; i < MaxTokenLength; i++)
        {
            if (i < tokens.Length)
            {
                inputIds[i] = tokens[i];
                attentionMask[i] = 1;
            }
            else
            {
                inputIds[i] = 0; // PAD token
                attentionMask[i] = 0;
            }
        }

        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, MaxTokenLength });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, MaxTokenLength });
        
        var inputs = new List<NamedOnnxValue> 
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };
        
        using var results = _textSession!.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();
        
        // Get the [CLS] token embedding (first token) and expand to match vision embedding size
        var textEmbedding = new float[EmbeddingSize];
        var baseEmbedding = output.Take(768).ToArray(); // CLIP text embedding is 768 dimensions
        
        // Repeat the 768-dim embedding to match 38400 dimensions (50 * 768)
        for (int i = 0; i < EmbeddingSize; i++)
        {
            textEmbedding[i] = baseEmbedding[i % 768];
        }
        
        return textEmbedding;
    }

    private long[] TokenizeText(string text)
    {
        // Simple tokenization - in production, use the full CLIP tokenizer
        // For now, simulate basic tokenization
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<long> { 49406 }; // Start token
        
        // Simple vocabulary mapping (in production, load from tokenizer vocab)
        var vocab = new Dictionary<string, long>
        {
            { "dog", 1929 }, { "cat", 2368 }, { "tree", 3392 }, { "water", 1265 },
            { "car", 1032 }, { "person", 2711 }, { "sky", 5025 }, { "building", 2258 },
            { "food", 2057 }, { "flower", 6546 }, { "house", 1797 }, { "animal", 4477 },
            { "nature", 3239 }, { "city", 1953 }, { "beach", 5094 }, { "mountain", 4422 },
            { "forest", 5509 }, { "lake", 6002 }, { "river", 6830 }, { "sunset", 12682 },
            { "portrait", 12636 }, { "landscape", 8688 }, { "street", 2904 }, { "road", 3060 }
        };
        
        foreach (var word in words.Take(75)) // Leave room for start/end tokens
        {
            if (vocab.TryGetValue(word, out var tokenId))
            {
                tokens.Add(tokenId);
            }
            else
            {
                tokens.Add(320); // Unknown token
            }
        }
        
        tokens.Add(49407); // End token
        return tokens.ToArray();
    }

    private float[] GenerateTextEmbeddingFromString(string text)
    {
        var embedding = new float[EmbeddingSize];
        
        // Use SHA256 to create a deterministic hash from the text
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.ToLowerInvariant()));
        
        // Generate embedding based on hash and text characteristics
        for (int i = 0; i < EmbeddingSize; i++)
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
        // Enhanced semantic grouping for common photo-related terms
        var semanticGroups = new Dictionary<string[], float[]>
        {
            { new[] { "dog", "dogs", "puppy", "puppies", "pet", "pets", "canine" }, new float[] { 0.8f, 0.2f, 0.5f, 0.9f, 0.7f, 0.3f, 0.6f, 0.4f } },
            { new[] { "cat", "cats", "kitten", "kittens", "feline" }, new float[] { 0.7f, 0.3f, 0.6f, 0.8f, 0.5f, 0.9f, 0.2f, 0.4f } },
            { new[] { "tree", "trees", "forest", "woods", "nature", "plant", "leaf", "leaves" }, new float[] { 0.1f, 0.9f, 0.2f, 0.7f, 0.6f, 0.4f, 0.8f, 0.3f } },
            { new[] { "water", "ocean", "sea", "lake", "river", "beach", "wave", "waves" }, new float[] { 0.2f, 0.1f, 0.8f, 0.6f, 0.9f, 0.3f, 0.7f, 0.5f } },
            { new[] { "car", "cars", "vehicle", "automobile", "truck", "road", "street" }, new float[] { 0.9f, 0.1f, 0.3f, 0.4f, 0.8f, 0.6f, 0.2f, 0.7f } },
            { new[] { "person", "people", "human", "man", "woman", "child", "face", "portrait" }, new float[] { 0.5f, 0.7f, 0.9f, 0.3f, 0.8f, 0.1f, 0.6f, 0.4f } },
            { new[] { "sky", "cloud", "clouds", "blue", "sunset", "sunrise" }, new float[] { 0.3f, 0.8f, 0.1f, 0.9f, 0.5f, 0.7f, 0.2f, 0.6f } },
            { new[] { "building", "house", "architecture", "city", "urban" }, new float[] { 0.6f, 0.2f, 0.9f, 0.4f, 0.7f, 0.1f, 0.8f, 0.3f } },
            { new[] { "food", "meal", "eating", "restaurant", "kitchen" }, new float[] { 0.4f, 0.9f, 0.2f, 0.6f, 0.8f, 0.3f, 0.7f, 0.1f } },
            { new[] { "flower", "flowers", "garden", "bloom", "petal" }, new float[] { 0.8f, 0.4f, 0.6f, 0.2f, 0.9f, 0.7f, 0.1f, 0.5f } }
        };

        var textLower = text.ToLowerInvariant();
        
        foreach (var group in semanticGroups)
        {
            foreach (var keyword in group.Key)
            {
                if (textLower.Contains(keyword))
                {
                    // Apply semantic pattern to embedding
                    for (int i = 0; i < Math.Min(group.Value.Length, 100); i++)
                    {
                        var targetIndex = (i * 384) % EmbeddingSize; // Spread pattern across embedding
                        embedding[targetIndex] = (embedding[targetIndex] + group.Value[i]) * 0.7f;
                        
                        // Also apply to nearby positions for smoother pattern
                        if (targetIndex + 1 < EmbeddingSize)
                            embedding[targetIndex + 1] = (embedding[targetIndex + 1] + group.Value[i] * 0.5f) * 0.8f;
                        if (targetIndex + 2 < EmbeddingSize)
                            embedding[targetIndex + 2] = (embedding[targetIndex + 2] + group.Value[i] * 0.3f) * 0.9f;
                    }
                    break; // Only apply first matching pattern per group
                }
            }
        }
        
        // Normalize the final embedding to prevent values from growing too large
        var norm = Math.Sqrt(embedding.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)(embedding[i] / norm);
            }
        }
    }

    public void Dispose()
    {
        _visionSession?.Dispose();
        _textSession?.Dispose();
    }
}