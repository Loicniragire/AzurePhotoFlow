using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace AzurePhotoFlow.Services;

public class OnnxImageEmbeddingModel : IImageEmbeddingModel
{
    private readonly InferenceSession _visionSession;
    private readonly InferenceSession? _textSession;
    private readonly Dictionary<string, object>? _tokenizer;
    private readonly ILogger<OnnxImageEmbeddingModel> _logger;
    private const int InputSize = 224;
    private const int EmbeddingSize = 512; // Standard CLIP embedding size
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
        
        // Extract the proper 512-dimensional CLIP embedding
        // CLIP vision models typically output 512-dimensional embeddings
        return output.Take(EmbeddingSize).ToArray();
    }

    public float[] GenerateTextEmbedding(string text)
    {
        // Require text model to be available - no fallback
        if (_textSession == null || _tokenizer == null)
        {
            _logger.LogError("[EMBEDDING DEBUG] No text model available for text embedding generation: '{Text}'", text);
            throw new InvalidOperationException("CLIP text model is required for text embedding generation. Please ensure the text model is properly loaded.");
        }

        try
        {
            _logger.LogInformation("[EMBEDDING DEBUG] Using CLIP text model for text embedding generation: '{Text}'", text);
            return GenerateRealTextEmbedding(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMBEDDING DEBUG] CLIP text model failed for text: '{Text}'", text);
            throw new InvalidOperationException($"Failed to generate text embedding: {ex.Message}", ex);
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
        
        // Extract the proper 512-dimensional CLIP text embedding
        // CLIP text models should output 512-dimensional embeddings in the same space as vision embeddings
        var textEmbedding = output.Take(EmbeddingSize).ToArray();
        
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



    public void Dispose()
    {
        _visionSession?.Dispose();
        _textSession?.Dispose();
    }
}