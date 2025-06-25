using System.Text;
using System.Text.Json;

namespace AzurePhotoFlow.Api.Services;

/// <summary>
/// CLIP tokenizer implementation using Byte Pair Encoding (BPE) for accurate text processing
/// compatible with the official CLIP text model
/// </summary>
public class ClipTokenizer
{
    private readonly Dictionary<string, int> _vocabulary;
    private readonly List<(string, string)> _merges;
    private readonly Dictionary<string, string> _cache;
    private const int MaxTokenLength = 77;
    private const int BosTokenId = 49406; // <|startoftext|>
    private const int EosTokenId = 49407; // <|endoftext|>
    private const int UnkTokenId = 49407; // Same as EOS for CLIP

    public ClipTokenizer(string tokenizerPath)
    {
        _cache = new Dictionary<string, string>();
        _vocabulary = LoadVocabulary(Path.Combine(tokenizerPath, "vocab.json"));
        _merges = LoadMerges(Path.Combine(tokenizerPath, "merges.txt"));
    }

    /// <summary>
    /// Tokenizes text using CLIP's BPE tokenizer
    /// </summary>
    public long[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new long[] { BosTokenId, EosTokenId };
        }

        // Basic text preprocessing
        text = text.Trim().ToLowerInvariant();
        
        var tokens = new List<long> { BosTokenId };
        var words = SplitText(text);

        foreach (var word in words)
        {
            if (tokens.Count >= MaxTokenLength - 1) break; // Leave room for EOS token
            
            var wordTokens = EncodeWord(word);
            tokens.AddRange(wordTokens.Select(t => (long)t));
        }

        // Ensure we don't exceed max length
        if (tokens.Count >= MaxTokenLength)
        {
            tokens = tokens.Take(MaxTokenLength - 1).ToList();
        }

        tokens.Add(EosTokenId);
        return tokens.Select(t => (long)t).ToArray();
    }

    /// <summary>
    /// Splits text into words following CLIP's approach
    /// </summary>
    private List<string> SplitText(string text)
    {
        // CLIP uses a specific regex pattern for tokenization
        // This is a simplified version that handles most common cases
        var words = new List<string>();
        var currentWord = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            if (char.IsWhiteSpace(c))
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
            }
            else if (char.IsPunctuation(c))
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                }
                words.Add(c.ToString());
            }
            else
            {
                currentWord.Append(c);
            }
        }

        if (currentWord.Length > 0)
        {
            words.Add(currentWord.ToString());
        }

        return words;
    }

    /// <summary>
    /// Encodes a single word using BPE
    /// </summary>
    private List<int> EncodeWord(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return new List<int>();
        }

        // Check cache first
        if (_cache.TryGetValue(word, out var cachedResult))
        {
            return ParseBpeString(cachedResult);
        }

        // Apply BPE
        var bpeResult = ApplyBpe(word);
        _cache[word] = bpeResult;

        return ParseBpeString(bpeResult);
    }

    /// <summary>
    /// Applies Byte Pair Encoding to a word
    /// </summary>
    private string ApplyBpe(string word)
    {
        if (word.Length <= 1)
        {
            return word;
        }

        // Initialize with character-level tokens
        var pairs = new List<string>();
        for (int i = 0; i < word.Length - 1; i++)
        {
            pairs.Add(word[i].ToString() + word[i + 1].ToString());
        }

        if (pairs.Count == 0)
        {
            return word;
        }

        // Apply BPE merges
        while (true)
        {
            var bigram = GetMostFrequentPair(pairs);
            if (bigram == null) break;

            var newPairs = new List<string>();
            int i = 0;
            while (i < pairs.Count)
            {
                if (i < pairs.Count - 1 && bigram.HasValue &&
                    pairs[i] + pairs[i + 1].Substring(pairs[i + 1].Length - 1) == bigram.Value.Item1 + bigram.Value.Item2)
                {
                    newPairs.Add(bigram.Value.Item1 + bigram.Value.Item2);
                    i += 2;
                }
                else
                {
                    newPairs.Add(pairs[i]);
                    i++;
                }
            }

            pairs = newPairs;
            if (pairs.Count == 1) break;
        }

        return string.Join(" ", pairs);
    }

    /// <summary>
    /// Finds the most frequent pair from available merges
    /// </summary>
    private (string, string)? GetMostFrequentPair(List<string> pairs)
    {
        foreach (var merge in _merges)
        {
            for (int i = 0; i < pairs.Count - 1; i++)
            {
                if (pairs[i] == merge.Item1 && pairs[i + 1] == merge.Item2)
                {
                    return merge;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Converts BPE string to token IDs
    /// </summary>
    private List<int> ParseBpeString(string bpeString)
    {
        var tokens = new List<int>();
        var subwords = bpeString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var subword in subwords)
        {
            if (_vocabulary.TryGetValue(subword, out var tokenId))
            {
                tokens.Add(tokenId);
            }
            else if (_vocabulary.TryGetValue(subword + "</w>", out var endWordTokenId))
            {
                tokens.Add(endWordTokenId);
            }
            else
            {
                // Handle character-level fallback
                foreach (char c in subword)
                {
                    var charKey = c.ToString();
                    if (_vocabulary.TryGetValue(charKey, out var charTokenId))
                    {
                        tokens.Add(charTokenId);
                    }
                    else
                    {
                        tokens.Add(UnkTokenId);
                    }
                }
            }
        }

        return tokens;
    }

    /// <summary>
    /// Loads vocabulary from vocab.json
    /// </summary>
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

    /// <summary>
    /// Loads BPE merges from merges.txt
    /// </summary>
    private List<(string, string)> LoadMerges(string mergesPath)
    {
        if (!File.Exists(mergesPath))
        {
            throw new FileNotFoundException($"Merges file not found: {mergesPath}");
        }

        var merges = new List<(string, string)>();
        var lines = File.ReadAllLines(mergesPath);

        // Skip header line if present
        int startIndex = lines.Length > 0 && lines[0].StartsWith("#") ? 1 : 0;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                merges.Add((parts[0], parts[1]));
            }
        }

        return merges;
    }

    /// <summary>
    /// Gets the maximum token length supported by CLIP
    /// </summary>
    public int GetMaxTokenLength() => MaxTokenLength;

    /// <summary>
    /// Gets special token IDs
    /// </summary>
    public (int Bos, int Eos, int Unk) GetSpecialTokens() => (BosTokenId, EosTokenId, UnkTokenId);
}