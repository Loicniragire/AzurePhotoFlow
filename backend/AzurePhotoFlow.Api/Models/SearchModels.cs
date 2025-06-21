using System.Text.Json.Serialization;

namespace Api.Models;


public class ComplexSearchRequest
{
    public string? SemanticQuery { get; set; }
    public string? SimilarityReferenceKey { get; set; }
    public int Limit { get; set; } = 20;
    public double Threshold { get; set; } = 0.5;
    public SearchCombinationMode CombinationMode { get; set; } = SearchCombinationMode.WeightedCombination;
    public double SemanticWeight { get; set; } = 0.5;
    public double SimilarityWeight { get; set; } = 0.5;
    public SearchFilters? Filters { get; set; }
}

public enum SearchCombinationMode
{
    Union,
    Intersection,
    WeightedCombination
}

public class SearchFilters
{
    public List<string>? ProjectNames { get; set; }
    public List<string>? Years { get; set; }
    public List<string>? DirectoryNames { get; set; }
    public List<string>? FileExtensions { get; set; }
    public UploadDateRange? UploadDateRange { get; set; }
    public Dictionary<string, object>? CustomFilters { get; set; }
}

public class UploadDateRange
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class SemanticSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public List<SemanticSearchResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public long TotalImagesSearched { get; set; }
    public string? CollectionName { get; set; }
    public long ProcessingTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SimilaritySearchResponse
{
    public string ReferenceObjectKey { get; set; } = string.Empty;
    public List<SimilaritySearchResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public long ProcessingTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ComplexSearchResponse
{
    public ComplexSearchRequest? Request { get; set; }
    public List<ComplexSearchResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public long ProcessingTimeMs { get; set; }
    public SearchResultBreakdown? Breakdown { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SemanticSearchResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ProjectName { get; set; }
    public string? DirectoryName { get; set; }
    public string? Year { get; set; }
    public DateTime? UploadDate { get; set; }
    public string? ImageUrl { get; set; }
    public double SimilarityScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class SimilaritySearchResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ProjectName { get; set; }
    public string? DirectoryName { get; set; }
    public string? Year { get; set; }
    public DateTime? UploadDate { get; set; }
    public string? ImageUrl { get; set; }
    public double SimilarityScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ComplexSearchResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ProjectName { get; set; }
    public string? DirectoryName { get; set; }
    public string? Year { get; set; }
    public DateTime? UploadDate { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public double? SemanticScore { get; set; }
    public double? SimilarityScore { get; set; }
    public double RelevanceScore { get; set; }
    public List<string> MatchedSearchTypes { get; set; } = new();
}

public class SearchResultBreakdown
{
    public int SemanticResults { get; set; }
    public int SimilarityResults { get; set; }
    public int OverlapResults { get; set; }
    public long SemanticSearchTimeMs { get; set; }
    public long SimilaritySearchTimeMs { get; set; }
    public long CombinationTimeMs { get; set; }
}

public class VectorSearchResult
{
    public string ObjectKey { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class CountResponse
{
    public long Count { get; set; }
    public string? CollectionName { get; set; }
    public Dictionary<string, object>? Filters { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
