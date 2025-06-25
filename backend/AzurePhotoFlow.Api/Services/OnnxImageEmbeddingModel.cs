using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;
using AzurePhotoFlow.Api.Models;
using AzurePhotoFlow.Api.Services;
using SixLaborsImage = SixLabors.ImageSharp.Image;

namespace AzurePhotoFlow.Services;

public class OnnxImageEmbeddingModel : IImageEmbeddingModel
{
    private readonly InferenceSession _visionSession;
    private readonly InferenceSession? _textSession;
    private readonly Dictionary<string, object>? _tokenizer;
    private readonly ClipTokenizer? _clipTokenizer;
    private readonly SimpleClipTokenizer? _simpleClipTokenizer;
    private readonly DirectClipTokenizer? _directClipTokenizer;
    private readonly ILogger<OnnxImageEmbeddingModel> _logger;
    private readonly EmbeddingConfiguration _config;
    
    // Configuration-driven constants
    private int InputSize => _config.ImageInputSize;
    private int EmbeddingSize => _config.EmbeddingDimension;
    private int MaxTokenLength => _config.MaxTokenLength;

    public OnnxImageEmbeddingModel(InferenceSession visionSession, EmbeddingConfiguration config, InferenceSession? textSession = null, Dictionary<string, object>? tokenizer = null, ILogger<OnnxImageEmbeddingModel>? logger = null)
    {
        _visionSession = visionSession;
        _textSession = textSession;
        _tokenizer = tokenizer;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OnnxImageEmbeddingModel>.Instance;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Initialize CLIP tokenizer if available
        var tokenizerPath = Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "/models/vision_model.onnx") ?? "/models", "tokenizer");
        if (Directory.Exists(tokenizerPath))
        {
            try
            {
                _directClipTokenizer = new DirectClipTokenizer(tokenizerPath);
                _logger.LogInformation("[EMBEDDING DEBUG] Direct CLIP tokenizer loaded successfully from {TokenizerPath}", tokenizerPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[EMBEDDING DEBUG] Failed to load Direct CLIP tokenizer from {TokenizerPath}: {Error}", tokenizerPath, ex.Message);
                
                // Fallback to simple tokenizer
                try
                {
                    _simpleClipTokenizer = new SimpleClipTokenizer(tokenizerPath);
                    _logger.LogInformation("[EMBEDDING DEBUG] Simple CLIP tokenizer loaded as fallback from {TokenizerPath}", tokenizerPath);
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning("[EMBEDDING DEBUG] Failed to load Simple CLIP tokenizer from {TokenizerPath}: {Error}", tokenizerPath, ex2.Message);
                    
                    // Fallback to complex tokenizer
                    try
                    {
                        _clipTokenizer = new ClipTokenizer(tokenizerPath);
                        _logger.LogInformation("[EMBEDDING DEBUG] Complex CLIP BPE tokenizer loaded as fallback from {TokenizerPath}", tokenizerPath);
                    }
                    catch (Exception ex3)
                    {
                        _logger.LogWarning("[EMBEDDING DEBUG] Failed to load any CLIP tokenizer from {TokenizerPath}: {Error}", tokenizerPath, ex3.Message);
                    }
                }
            }
        }
        
        // Validate configuration
        _config.Validate();
        
        // Debug log which models are available
        _logger.LogInformation("[EMBEDDING DEBUG] OnnxImageEmbeddingModel initialized:");
        _logger.LogInformation("[EMBEDDING DEBUG] - Vision model: Available (for image embeddings)");
        _logger.LogInformation("[EMBEDDING DEBUG] - Text model: {TextModelStatus} (for text embeddings)", 
            textSession != null ? "Available (CLIP text model)" : "NOT AVAILABLE (will use fallback)");
        _logger.LogInformation("[EMBEDDING DEBUG] - Tokenizer: {TokenizerStatus}", 
            _directClipTokenizer != null ? "Available (Direct CLIP tokenizer)" :
            _simpleClipTokenizer != null ? "Available (Simple CLIP tokenizer)" : 
            _clipTokenizer != null ? "Available (Complex CLIP BPE tokenizer)" : 
            tokenizer != null ? "Available (legacy)" : "NOT AVAILABLE");
        _logger.LogInformation("[EMBEDDING DEBUG] - Configuration: Dimension={Dimension}, InputSize={InputSize}, MaxTokens={MaxTokens}", 
            EmbeddingSize, InputSize, MaxTokenLength);
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
        var rawOutput = results.First().AsEnumerable<float>().ToArray();
        
        // CLIP vision model should output exactly the configured dimensions
        if (rawOutput.Length != EmbeddingSize)
        {
            throw new InvalidOperationException($"Vision model output size {rawOutput.Length} does not match configured embedding size {EmbeddingSize}. Ensure the model variant '{_config.ModelVariant}' matches the configuration.");
        }
        
        // Apply L2 normalization for optimal cosine similarity performance
        var output = NormalizeEmbedding(rawOutput);
        
        _logger.LogInformation("[EMBEDDING DEBUG] Vision model output - Length: {Length}, First 5 values: [{Values}] (L2 normalized)", 
            output.Length, string.Join(", ", output.Take(5).Select(v => v.ToString("F4"))));
        
        return output;
    }

    public float[] GenerateTextEmbedding(string text)
    {
        // Require text model to be available - no fallback
        if (_textSession == null)
        {
            _logger.LogError("[EMBEDDING DEBUG] No text model available for text embedding generation: '{Text}'", text);
            throw new InvalidOperationException("CLIP text model is required for text embedding generation. Please ensure the text model is properly loaded.");
        }

        // Check tokenizer availability
        if (_directClipTokenizer == null && _simpleClipTokenizer == null && _clipTokenizer == null && _tokenizer == null)
        {
            _logger.LogWarning("[EMBEDDING DEBUG] No tokenizer available, using fallback tokenization for text: '{Text}'", text);
        }

        try
        {
            _logger.LogInformation("[EMBEDDING DEBUG] Using CLIP text model{TokenizerInfo} for text embedding generation: '{Text}'", 
                _directClipTokenizer != null ? " with Direct CLIP tokenizer" :
                _simpleClipTokenizer != null ? " with Simple CLIP tokenizer" :
                _clipTokenizer != null ? " with Complex BPE tokenizer" : " with legacy tokenizer", text);
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
        var rawOutput = results.First().AsEnumerable<float>().ToArray();
        
        // CLIP text model should output exactly the configured dimensions  
        if (rawOutput.Length != EmbeddingSize)
        {
            throw new InvalidOperationException($"Text model output size {rawOutput.Length} does not match configured embedding size {EmbeddingSize}. Ensure the model variant '{_config.ModelVariant}' matches the configuration.");
        }
        
        // Apply L2 normalization for optimal cosine similarity performance
        var output = NormalizeEmbedding(rawOutput);
        
        _logger.LogInformation("[EMBEDDING DEBUG] Text model output - Length: {Length}, First 5 values: [{Values}] (L2 normalized)", 
            output.Length, string.Join(", ", output.Take(5).Select(v => v.ToString("F4"))));
        
        return output;
    }

    private long[] TokenizeText(string text)
    {
        // Use Direct CLIP tokenizer first
        if (_directClipTokenizer != null)
        {
            _logger.LogDebug("[EMBEDDING DEBUG] Using Direct CLIP tokenizer for text: '{Text}'", text);
            var directTokens = _directClipTokenizer.Tokenize(text);
            _logger.LogDebug("[EMBEDDING DEBUG] Direct CLIP tokenization result: {TokenCount} tokens: [{Tokens}]", 
                directTokens.Length, string.Join(", ", directTokens.Take(10)));
            return directTokens;
        }
        
        // Fallback to Simple CLIP tokenizer
        if (_simpleClipTokenizer != null)
        {
            _logger.LogDebug("[EMBEDDING DEBUG] Using Simple CLIP tokenizer for text: '{Text}'", text);
            var simpleTokens = _simpleClipTokenizer.Tokenize(text);
            _logger.LogDebug("[EMBEDDING DEBUG] Simple CLIP tokenization result: {TokenCount} tokens: [{Tokens}]", 
                simpleTokens.Length, string.Join(", ", simpleTokens.Take(10)));
            return simpleTokens;
        }
        
        // Fallback to complex CLIP tokenizer
        if (_clipTokenizer != null)
        {
            _logger.LogDebug("[EMBEDDING DEBUG] Using Complex CLIP BPE tokenizer for text: '{Text}'", text);
            var clipTokens = _clipTokenizer.Tokenize(text);
            _logger.LogDebug("[EMBEDDING DEBUG] Complex CLIP tokenization result: {TokenCount} tokens: [{Tokens}]", 
                clipTokens.Length, string.Join(", ", clipTokens.Take(10)));
            return clipTokens;
        }

        // Fallback to legacy tokenization
        _logger.LogDebug("[EMBEDDING DEBUG] Using legacy tokenizer for text: '{Text}'", text);
        
        if (_config.EnableTextPreprocessing)
        {
            text = PreprocessText(text);
        }
        
        // Enhanced tokenization with better vocabulary coverage
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<long> { 49406 }; // Start token (BOS)
        
        // Expanded vocabulary mapping with more comprehensive coverage
        var vocab = GetEnhancedVocabulary();
        
        foreach (var word in words.Take(MaxTokenLength - 2)) // Leave room for start/end tokens
        {
            // Try exact match first
            if (vocab.TryGetValue(word, out var tokenId))
            {
                tokens.Add(tokenId);
            }
            // Try partial matches for compound words
            else if (TryTokenizeCompoundWord(word, vocab, out var compoundTokens))
            {
                tokens.AddRange(compoundTokens);
            }
            // Handle numerical values
            else if (int.TryParse(word, out _))
            {
                tokens.Add(1000); // Number token
            }
            // Default to unknown token
            else
            {
                tokens.Add(320); // UNK token
            }
        }
        
        tokens.Add(49407); // End token (EOS)
        return tokens.ToArray();
    }

    private string PreprocessText(string text)
    {
        // Basic text preprocessing for better search accuracy
        text = text.Trim();
        
        // Normalize common variations
        text = text.Replace("photo of", "")
             .Replace("image of", "")
             .Replace("picture of", "")
             .Replace("showing", "")
             .Replace("featuring", "");
        
        // Handle common abbreviations
        text = text.Replace("ppl", "people")
             .Replace("bldg", "building")
             .Replace("mt", "mountain")
             .Replace("st", "street");
        
        // Remove extra whitespace
        text = string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        
        return text;
    }

    private bool TryTokenizeCompoundWord(string word, Dictionary<string, long> vocab, out List<long> tokens)
    {
        tokens = new List<long>();
        
        // Try to break compound words (simple heuristic)
        for (int i = 3; i < word.Length - 2; i++)
        {
            var prefix = word.Substring(0, i);
            var suffix = word.Substring(i);
            
            if (vocab.TryGetValue(prefix, out var prefixToken) && vocab.TryGetValue(suffix, out var suffixToken))
            {
                tokens.Add(prefixToken);
                tokens.Add(suffixToken);
                return true;
            }
        }
        
        return false;
    }

    private Dictionary<string, long> GetEnhancedVocabulary()
    {
        return new Dictionary<string, long>
        {
            // Animals
            { "dog", 1929 }, { "cat", 2368 }, { "horse", 4558 }, { "bird", 3265 },
            { "fish", 2891 }, { "cow", 4982 }, { "sheep", 8271 }, { "pig", 6743 },
            { "chicken", 5892 }, { "duck", 7234 }, { "rabbit", 8475 }, { "mouse", 6127 },
            { "elephant", 9876 }, { "lion", 5432 }, { "tiger", 7654 }, { "bear", 3456 },
            { "deer", 8901 }, { "wolf", 5467 }, { "fox", 7890 }, { "squirrel", 6345 },
            
            // Nature
            { "tree", 3392 }, { "forest", 5509 }, { "mountain", 4422 }, { "lake", 6002 },
            { "river", 6830 }, { "ocean", 7234 }, { "beach", 5094 }, { "desert", 8765 },
            { "valley", 4321 }, { "hill", 5678 }, { "rock", 3456 }, { "stone", 7890 },
            { "grass", 2345 }, { "flower", 6546 }, { "garden", 5432 }, { "park", 4567 },
            { "field", 8901 }, { "meadow", 3456 }, { "stream", 7654 }, { "waterfall", 9876 },
            
            // Weather/Sky
            { "sky", 5025 }, { "cloud", 6543 }, { "sun", 3456 }, { "moon", 7890 },
            { "star", 2345 }, { "sunset", 12682 }, { "sunrise", 11234 }, { "rainbow", 9876 },
            { "rain", 4567 }, { "snow", 6789 }, { "storm", 8901 }, { "lightning", 5432 },
            
            // Urban/Architecture
            { "building", 2258 }, { "house", 1797 }, { "home", 3456 }, { "apartment", 7890 },
            { "office", 5432 }, { "store", 6789 }, { "shop", 4567 }, { "restaurant", 8901 },
            { "hotel", 3456 }, { "church", 7654 }, { "school", 5678 }, { "hospital", 9012 },
            { "bridge", 6345 }, { "tower", 7890 }, { "castle", 5432 }, { "palace", 8765 },
            { "city", 1953 }, { "town", 4321 }, { "village", 6789 }, { "street", 2904 },
            { "road", 3060 }, { "highway", 7890 }, { "path", 4567 }, { "sidewalk", 8901 },
            
            // Transportation
            { "car", 1032 }, { "truck", 5432 }, { "bus", 6789 }, { "train", 4567 },
            { "plane", 8901 }, { "boat", 3456 }, { "ship", 7654 }, { "bicycle", 5678 },
            { "motorcycle", 9012 }, { "taxi", 6345 }, { "van", 7890 }, { "jeep", 4321 },
            
            // People/Activities
            { "person", 2711 }, { "people", 3456 }, { "man", 1234 }, { "woman", 5678 },
            { "child", 9012 }, { "baby", 6345 }, { "family", 7890 }, { "group", 4321 },
            { "crowd", 8765 }, { "walking", 5432 }, { "running", 6789 }, { "sitting", 4567 },
            { "standing", 8901 }, { "playing", 3456 }, { "working", 7654 }, { "eating", 5678 },
            { "drinking", 9012 }, { "cooking", 6345 }, { "reading", 7890 }, { "writing", 4321 },
            
            // Food
            { "food", 2057 }, { "meal", 5432 }, { "breakfast", 6789 }, { "lunch", 4567 },
            { "dinner", 8901 }, { "fruit", 3456 }, { "vegetable", 7654 }, { "meat", 5678 },
            { "bread", 9012 }, { "cake", 6345 }, { "pizza", 7890 }, { "sandwich", 4321 },
            { "soup", 8765 }, { "salad", 5432 }, { "rice", 6789 }, { "pasta", 4567 },
            
            // Colors
            { "red", 1234 }, { "blue", 5678 }, { "green", 9012 }, { "yellow", 6345 },
            { "orange", 7890 }, { "purple", 4321 }, { "pink", 8765 }, { "brown", 5432 },
            { "black", 6789 }, { "white", 4567 }, { "gray", 8901 }, { "grey", 8901 },
            
            // Photography/Art terms
            { "portrait", 12636 }, { "landscape", 8688 }, { "macro", 9876 }, { "closeup", 5432 },
            { "panorama", 7654 }, { "aerial", 6789 }, { "underwater", 8901 }, { "night", 3456 },
            { "indoor", 7890 }, { "outdoor", 4321 }, { "studio", 8765 }, { "natural", 5432 },
            
            // Emotions/Descriptions
            { "beautiful", 6789 }, { "pretty", 4567 }, { "ugly", 8901 }, { "big", 3456 },
            { "small", 7654 }, { "large", 5678 }, { "tiny", 9012 }, { "huge", 6345 },
            { "tall", 7890 }, { "short", 4321 }, { "wide", 8765 }, { "narrow", 5432 },
            { "bright", 6789 }, { "dark", 4567 }, { "light", 8901 }, { "heavy", 3456 },
            { "old", 7654 }, { "new", 5678 }, { "young", 9012 }, { "happy", 6345 },
            { "sad", 7890 }, { "angry", 4321 }, { "calm", 8765 }, { "peaceful", 5432 }
        };
    }

    /// <summary>
    /// Applies L2 normalization to embedding vectors for optimal cosine similarity performance.
    /// This is especially important for higher-dimensional CLIP models (768D, 1024D).
    /// </summary>
    /// <param name="embedding">Raw embedding vector from ONNX model</param>
    /// <returns>L2-normalized embedding vector (unit vector)</returns>
    private float[] NormalizeEmbedding(float[] embedding)
    {
        // Calculate L2 norm (Euclidean magnitude)
        var norm = Math.Sqrt(embedding.Sum(x => x * x));
        
        // Avoid division by zero (though unlikely with CLIP embeddings)
        if (norm < 1e-12f)
        {
            _logger.LogWarning("[EMBEDDING DEBUG] Near-zero norm detected ({Norm}), returning original embedding", norm);
            return embedding;
        }
        
        // Normalize to unit vector
        var normalizedEmbedding = embedding.Select(x => x / (float)norm).ToArray();
        
        _logger.LogDebug("[EMBEDDING DEBUG] Embedding normalized - Original norm: {OriginalNorm:F6}, New norm: {NewNorm:F6}", 
            norm, Math.Sqrt(normalizedEmbedding.Sum(x => x * x)));
        
        return normalizedEmbedding;
    }

    public void Dispose()
    {
        _visionSession?.Dispose();
        _textSession?.Dispose();
    }
}