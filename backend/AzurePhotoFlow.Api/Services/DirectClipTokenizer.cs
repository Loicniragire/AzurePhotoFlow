using System.Text.Json;

namespace AzurePhotoFlow.Api.Services;

/// <summary>
/// Direct CLIP tokenizer that uses vocabulary lookup without complex BPE processing
/// </summary>
public class DirectClipTokenizer
{
    private readonly Dictionary<string, int> _vocabulary;
    private const int MaxTokenLength = 77;
    private const int BosTokenId = 49406; // <|startoftext|>
    private const int EosTokenId = 49407; // <|endoftext|>
    private const int UnkTokenId = 49407; // Same as EOS for CLIP

    public DirectClipTokenizer(string tokenizerPath)
    {
        _vocabulary = LoadVocabulary(Path.Combine(tokenizerPath, "vocab.json"));
    }

    /// <summary>
    /// Tokenizes text using direct vocabulary lookup (should produce correct token counts)
    /// </summary>
    public long[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new long[] { BosTokenId, EosTokenId };
        }

        // Basic preprocessing to match CLIP expectations
        text = text.Trim().ToLowerInvariant();
        
        var tokens = new List<long> { BosTokenId };
        var words = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (tokens.Count >= MaxTokenLength - 1) break; // Leave room for EOS token
            
            // Clean basic punctuation
            var cleanWord = word.Trim('!', '?', '.', ',', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}');
            
            // Try direct lookup strategies in order of preference
            if (_vocabulary.TryGetValue(cleanWord, out var wordTokenId))
            {
                // Direct word match first (often better than </w> tokens)
                tokens.Add(wordTokenId);
            }
            else if (_vocabulary.TryGetValue(cleanWord + "</w>", out var endWordTokenId))
            {
                // End-of-word tokens as fallback
                tokens.Add(endWordTokenId);
            }
            else if (_vocabulary.TryGetValue(" " + cleanWord, out var spaceWordTokenId))
            {
                // Try with leading space (common in CLIP vocab)
                tokens.Add(spaceWordTokenId);
            }
            else if (_vocabulary.TryGetValue(" " + cleanWord + "</w>", out var spaceEndWordTokenId))
            {
                // Try with leading space and end-of-word
                tokens.Add(spaceEndWordTokenId);
            }
            else
            {
                // For unknown words, add UNK token
                tokens.Add(UnkTokenId);
            }
        }

        // Ensure we don't exceed max length
        if (tokens.Count >= MaxTokenLength)
        {
            tokens = tokens.Take(MaxTokenLength - 1).ToList();
        }

        tokens.Add(EosTokenId);
        return tokens.ToArray();
    }

    private Dictionary<string, int> LoadVocabulary(string vocabPath)
    {
        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException($"Vocabulary file not found: {vocabPath}");
        }

        var jsonString = File.ReadAllText(vocabPath);
        var vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonString);
        
        if (vocab == null)
        {
            throw new InvalidOperationException("Failed to load vocabulary");
        }

        return vocab;
    }

    public int GetMaxTokenLength() => MaxTokenLength;
    public (int Bos, int Eos, int Unk) GetSpecialTokens() => (BosTokenId, EosTokenId, UnkTokenId);
}