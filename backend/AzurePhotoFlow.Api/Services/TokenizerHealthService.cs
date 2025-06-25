using System.Text.Json;
using AzurePhotoFlow.Api.Models;

namespace AzurePhotoFlow.Api.Services;

public class TokenizerHealthService
{
    private readonly ILogger<TokenizerHealthService> _logger;
    private readonly EmbeddingConfiguration _config;
    
    public TokenizerHealthService(ILogger<TokenizerHealthService> logger, EmbeddingConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
    
    public TokenizerHealthResult CheckTokenizerHealth()
    {
        var result = new TokenizerHealthResult();
        var tokenizerPath = GetTokenizerPath();
        
        _logger.LogInformation("[TOKENIZER HEALTH] Checking tokenizer health at: {TokenizerPath}", tokenizerPath);
        
        // Check if tokenizer directory exists
        if (!Directory.Exists(tokenizerPath))
        {
            result.IsHealthy = false;
            result.Issues.Add($"Tokenizer directory not found: {tokenizerPath}");
            _logger.LogError("[TOKENIZER HEALTH] Tokenizer directory not found: {TokenizerPath}", tokenizerPath);
            return result;
        }
        
        // Check required tokenizer files
        var requiredFiles = new Dictionary<string, string>
        {
            { "vocab.json", "CLIP vocabulary file containing token mappings" },
            { "merges.txt", "BPE merge rules for subword tokenization" },
            { "tokenizer_config.json", "Tokenizer configuration and metadata" },
            { "special_tokens_map.json", "Special token mappings and settings" }
        };
        
        foreach (var (fileName, description) in requiredFiles)
        {
            var filePath = Path.Combine(tokenizerPath, fileName);
            var fileInfo = ValidateTokenizerFile(filePath, fileName, description);
            result.FileValidations.Add(fileInfo);
            
            if (!fileInfo.IsValid)
            {
                result.IsHealthy = false;
            }
        }
        
        // Validate tokenizer configuration
        if (result.FileValidations.Any(f => f.FileName == "tokenizer_config.json" && f.IsValid))
        {
            ValidateTokenizerConfig(tokenizerPath, result);
        }
        
        // Validate vocabulary size
        if (result.FileValidations.Any(f => f.FileName == "vocab.json" && f.IsValid))
        {
            ValidateVocabularySize(tokenizerPath, result);
        }
        
        _logger.LogInformation("[TOKENIZER HEALTH] Health check completed. Status: {Status}, Issues: {IssueCount}", 
            result.IsHealthy ? "HEALTHY" : "UNHEALTHY", result.Issues.Count);
        
        return result;
    }
    
    private TokenizerFileValidation ValidateTokenizerFile(string filePath, string fileName, string description)
    {
        var validation = new TokenizerFileValidation
        {
            FileName = fileName,
            FilePath = filePath,
            Description = description
        };
        
        try
        {
            if (!File.Exists(filePath))
            {
                validation.IsValid = false;
                validation.ErrorMessage = "File not found";
                _logger.LogWarning("[TOKENIZER HEALTH] Missing file: {FileName} at {FilePath}", fileName, filePath);
                return validation;
            }
            
            var fileInfo = new FileInfo(filePath);
            validation.FileSizeBytes = fileInfo.Length;
            validation.LastModified = fileInfo.LastWriteTime;
            
            // Check if file is not empty
            if (fileInfo.Length == 0)
            {
                validation.IsValid = false;
                validation.ErrorMessage = "File is empty";
                _logger.LogWarning("[TOKENIZER HEALTH] Empty file: {FileName} at {FilePath}", fileName, filePath);
                return validation;
            }
            
            // Validate file format based on extension
            if (fileName.EndsWith(".json"))
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    JsonDocument.Parse(content);
                    validation.IsValid = true;
                    _logger.LogDebug("[TOKENIZER HEALTH] Valid JSON file: {FileName} ({Size} bytes)", 
                        fileName, fileInfo.Length);
                }
                catch (JsonException ex)
                {
                    validation.IsValid = false;
                    validation.ErrorMessage = $"Invalid JSON format: {ex.Message}";
                    _logger.LogError("[TOKENIZER HEALTH] Invalid JSON in {FileName}: {Error}", fileName, ex.Message);
                }
            }
            else if (fileName == "merges.txt")
            {
                // Basic validation for merges.txt format
                var lines = File.ReadLines(filePath).Take(10).ToList();
                if (lines.Count == 0 || !lines[0].StartsWith("#"))
                {
                    validation.IsValid = false;
                    validation.ErrorMessage = "Invalid merges.txt format (should start with #version header)";
                    _logger.LogWarning("[TOKENIZER HEALTH] Invalid merges.txt format in {FilePath}", filePath);
                }
                else
                {
                    validation.IsValid = true;
                    _logger.LogDebug("[TOKENIZER HEALTH] Valid merges.txt file: {FileName} ({Size} bytes)", 
                        fileName, fileInfo.Length);
                }
            }
            else
            {
                validation.IsValid = true;
                _logger.LogDebug("[TOKENIZER HEALTH] File exists: {FileName} ({Size} bytes)", 
                    fileName, fileInfo.Length);
            }
        }
        catch (Exception ex)
        {
            validation.IsValid = false;
            validation.ErrorMessage = $"Error accessing file: {ex.Message}";
            _logger.LogError(ex, "[TOKENIZER HEALTH] Error validating {FileName}: {Error}", fileName, ex.Message);
        }
        
        return validation;
    }
    
    private void ValidateTokenizerConfig(string tokenizerPath, TokenizerHealthResult result)
    {
        try
        {
            var configPath = Path.Combine(tokenizerPath, "tokenizer_config.json");
            var configContent = File.ReadAllText(configPath);
            var config = JsonDocument.Parse(configContent);
            
            // Check for expected tokenizer configuration
            if (config.RootElement.TryGetProperty("max_length", out var maxLengthElement))
            {
                var maxLength = maxLengthElement.GetInt32();
                if (maxLength != _config.MaxTokenLength)
                {
                    result.Issues.Add($"Tokenizer max_length ({maxLength}) doesn't match configuration ({_config.MaxTokenLength})");
                    _logger.LogWarning("[TOKENIZER HEALTH] Max length mismatch: tokenizer={MaxLengthTokenizer}, config={MaxLengthConfig}", 
                        maxLength, _config.MaxTokenLength);
                }
                else
                {
                    _logger.LogDebug("[TOKENIZER HEALTH] Max length validation passed: {MaxLength}", maxLength);
                }
            }
            
            // Check tokenizer class
            if (config.RootElement.TryGetProperty("tokenizer_class", out var tokenizerClassElement))
            {
                var tokenizerClass = tokenizerClassElement.GetString();
                if (tokenizerClass != "CLIPTokenizer")
                {
                    result.Issues.Add($"Unexpected tokenizer class: {tokenizerClass} (expected: CLIPTokenizer)");
                    _logger.LogWarning("[TOKENIZER HEALTH] Unexpected tokenizer class: {TokenizerClass}", tokenizerClass);
                }
                else
                {
                    _logger.LogDebug("[TOKENIZER HEALTH] Tokenizer class validation passed: {TokenizerClass}", tokenizerClass);
                }
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Failed to validate tokenizer configuration: {ex.Message}");
            _logger.LogError(ex, "[TOKENIZER HEALTH] Error validating tokenizer configuration");
        }
    }
    
    private void ValidateVocabularySize(string tokenizerPath, TokenizerHealthResult result)
    {
        try
        {
            var vocabPath = Path.Combine(tokenizerPath, "vocab.json");
            var vocabContent = File.ReadAllText(vocabPath);
            var vocab = JsonDocument.Parse(vocabContent);
            
            var vocabSize = vocab.RootElement.EnumerateObject().Count();
            result.VocabularySize = vocabSize;
            
            // CLIP typically has 49,408 tokens
            const int expectedVocabSize = 49408;
            if (vocabSize != expectedVocabSize)
            {
                result.Issues.Add($"Vocabulary size ({vocabSize}) differs from expected CLIP vocabulary size ({expectedVocabSize})");
                _logger.LogWarning("[TOKENIZER HEALTH] Vocabulary size mismatch: actual={ActualSize}, expected={ExpectedSize}", 
                    vocabSize, expectedVocabSize);
            }
            else
            {
                _logger.LogDebug("[TOKENIZER HEALTH] Vocabulary size validation passed: {VocabSize} tokens", vocabSize);
            }
            
            // Check for required special tokens
            var requiredTokens = new[] { "<|startoftext|>", "<|endoftext|>" };
            foreach (var token in requiredTokens)
            {
                if (!vocab.RootElement.TryGetProperty(token, out _))
                {
                    result.Issues.Add($"Missing required special token: {token}");
                    _logger.LogWarning("[TOKENIZER HEALTH] Missing special token: {Token}", token);
                }
                else
                {
                    _logger.LogDebug("[TOKENIZER HEALTH] Special token found: {Token}", token);
                }
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add($"Failed to validate vocabulary: {ex.Message}");
            _logger.LogError(ex, "[TOKENIZER HEALTH] Error validating vocabulary");
        }
    }
    
    private string GetTokenizerPath()
    {
        var modelPath = Environment.GetEnvironmentVariable("CLIP_MODEL_PATH") ?? "/models/vision_model.onnx";
        var modelsDir = Path.GetDirectoryName(modelPath) ?? "/models";
        return Path.Combine(modelsDir, "tokenizer");
    }
}

public class TokenizerHealthResult
{
    public bool IsHealthy { get; set; } = true;
    public List<string> Issues { get; set; } = new();
    public List<TokenizerFileValidation> FileValidations { get; set; } = new();
    public int? VocabularySize { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class TokenizerFileValidation
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime LastModified { get; set; }
}