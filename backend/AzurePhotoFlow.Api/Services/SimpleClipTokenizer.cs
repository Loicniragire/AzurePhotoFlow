using System.Text.Json;

namespace AzurePhotoFlow.Api.Services;

/// <summary>
/// Simplified CLIP tokenizer that directly uses vocabulary lookup for better compatibility
/// </summary>
public class SimpleClipTokenizer
{
    private readonly Dictionary<string, int> _vocabulary;
    private const int MaxTokenLength = 77;
    private const int BosTokenId = 49406; // <|startoftext|>
    private const int EosTokenId = 49407; // <|endoftext|>
    private const int UnkTokenId = 49407; // Same as EOS for CLIP

    public SimpleClipTokenizer(string tokenizerPath)
    {
        _vocabulary = LoadVocabulary(Path.Combine(tokenizerPath, "vocab.json"));
    }

    /// <summary>
    /// Tokenizes text using direct vocabulary lookup (simplified but more reliable)
    /// </summary>
    public long[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new long[] { BosTokenId, EosTokenId };
        }

        // Basic preprocessing
        text = text.Trim().ToLowerInvariant();
        
        var tokens = new List<long> { BosTokenId };
        var words = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (tokens.Count >= MaxTokenLength - 1) break; // Leave room for EOS token
            
            // Clean punctuation
            var cleanWord = word.Trim('!', '?', '.', ',', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}');
            
            // Try multiple vocabulary lookup strategies
            if (TryGetToken(cleanWord, out var tokenId))
            {
                tokens.Add(tokenId);
            }
            else if (TryGetToken(cleanWord + "</w>", out var endTokenId))
            {
                tokens.Add(endTokenId);
            }
            else
            {
                // Try splitting camelCase or compound words
                var subWords = SplitCompoundWord(cleanWord);
                bool found = false;
                
                foreach (var subWord in subWords)
                {
                    if (TryGetToken(subWord, out var subTokenId))
                    {
                        tokens.Add(subTokenId);
                        found = true;
                    }
                    else if (TryGetToken(subWord + "</w>", out var subEndTokenId))
                    {
                        tokens.Add(subEndTokenId);
                        found = true;
                    }
                }
                
                // Ultimate fallback: character level
                if (!found)
                {
                    foreach (char c in cleanWord.Take(3)) // Limit to first 3 chars to avoid too many tokens
                    {
                        if (TryGetToken(c.ToString(), out var charTokenId))
                        {
                            tokens.Add(charTokenId);
                        }
                        else
                        {
                            tokens.Add(UnkTokenId);
                        }
                        
                        if (tokens.Count >= MaxTokenLength - 1) break;
                    }
                }
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

    private bool TryGetToken(string text, out long tokenId)
    {
        if (_vocabulary.TryGetValue(text, out var id))
        {
            tokenId = id;
            return true;
        }
        tokenId = UnkTokenId;
        return false;
    }

    private List<string> SplitCompoundWord(string word)
    {
        var result = new List<string>();
        
        if (word.Length <= 3)
        {
            result.Add(word);
            return result;
        }

        // Try to split into meaningful parts
        for (int i = 2; i <= word.Length - 2; i++)
        {
            var prefix = word.Substring(0, i);
            var suffix = word.Substring(i);
            
            if (_vocabulary.ContainsKey(prefix) && _vocabulary.ContainsKey(suffix))
            {
                result.Add(prefix);
                result.Add(suffix);
                return result;
            }
        }
        
        // No good split found
        result.Add(word);
        return result;
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