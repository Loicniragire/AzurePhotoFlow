using Api.Interfaces;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Api.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public SearchController(
        ILogger<SearchController> logger, 
        IEmbeddingService embeddingService,
        IVectorStore vectorStore)
    {
        _logger = logger;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Perform semantic search for images using natural language queries.
    /// </summary>
    /// <param name="query">Natural language search query (e.g., "photos of dogs in parks")</param>
    /// <param name="limit">Maximum number of results to return (1-100, default: 20)</param>
    /// <param name="threshold">Minimum similarity threshold (0.0-1.0, default: 0.5)</param>
    /// <param name="projectName">Optional project name filter</param>
    /// <param name="year">Optional year filter</param>
    /// <returns>Semantic search results with similarity scores</returns>
    [HttpGet("semantic")]
    public async Task<ActionResult<SemanticSearchResponse>> SemanticSearch(
        [FromQuery] string query,
        [FromQuery] int limit = 20,
        [FromQuery] double threshold = 0.5,
        [FromQuery] string? projectName = null,
        [FromQuery] string? year = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new SemanticSearchResponse { Query = query };

        try
        {
            _logger.LogInformation("Semantic search request: Query='{Query}', Limit={Limit}, Threshold={Threshold}, Project={Project}, Year={Year}", 
                query, limit, threshold, projectName, year);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new SemanticSearchResponse 
                { 
                    Query = query ?? "", 
                    Success = false, 
                    ErrorMessage = "Search query cannot be empty" 
                });
            }

            if (limit < 1 || limit > 100)
            {
                return BadRequest(new SemanticSearchResponse 
                { 
                    Query = query, 
                    Success = false, 
                    ErrorMessage = "Limit must be between 1 and 100" 
                });
            }

            if (threshold < 0.0 || threshold > 1.0)
            {
                return BadRequest(new SemanticSearchResponse 
                { 
                    Query = query, 
                    Success = false, 
                    ErrorMessage = "Threshold must be between 0.0 and 1.0" 
                });
            }

            // Generate embedding for the search query
            _logger.LogDebug("Generating text embedding for query: {Query}", query);
            var queryEmbedding = await _embeddingService.GenerateTextEmbeddingAsync(query);

            // Build search filters
            var filters = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                filters["project_name"] = projectName;
            }
            if (!string.IsNullOrWhiteSpace(year))
            {
                filters["year"] = year;
            }

            // Get total count of images being searched
            var totalImagesSearched = await _vectorStore.GetTotalCountAsync(filters);

            // Perform vector similarity search
            _logger.LogDebug("Performing vector search with {Dimensions} dimensional query vector", queryEmbedding.Length);
            var vectorResults = await _vectorStore.SearchAsync(queryEmbedding, limit, threshold, filters);

            // Convert vector search results to semantic search results
            var searchResults = vectorResults.Select(vr => CreateSemanticSearchResult(vr)).ToList();

            // Populate response
            response.Results = searchResults;
            response.TotalResults = searchResults.Count;
            response.TotalImagesSearched = totalImagesSearched;
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            response.Success = true;

            _logger.LogInformation("Semantic search completed: Found {ResultCount} results in {ProcessingTimeMs}ms for query '{Query}'", 
                response.TotalResults, response.ProcessingTimeMs, query);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid semantic search request: {Query}", query);
            response.Success = false;
            response.ErrorMessage = ex.Message;
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing semantic search request: {Query}", query);
            response.Success = false;
            response.ErrorMessage = "Internal server error occurred while processing search request";
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Get search suggestions or perform advanced semantic search with detailed request model.
    /// </summary>
    /// <param name="request">Detailed semantic search request</param>
    /// <returns>Semantic search results</returns>
    [HttpPost("semantic")]
    public async Task<ActionResult<SemanticSearchResponse>> SemanticSearchPost([FromBody] SemanticSearchRequest request)
    {
        // Delegate to GET method with parameters from request body
        return await SemanticSearch(
            request.Query, 
            request.Limit, 
            request.Threshold, 
            request.ProjectName, 
            request.Year);
    }

    /// <summary>
    /// Find visually similar images based on a reference image.
    /// </summary>
    /// <param name="objectKey">Object key/path of the reference image</param>
    /// <param name="limit">Maximum number of results to return (1-100, default: 20)</param>
    /// <param name="threshold">Minimum similarity threshold (0.0-1.0, default: 0.5)</param>
    /// <param name="projectName">Optional project name filter</param>
    /// <param name="year">Optional year filter</param>
    /// <returns>Visual similarity search results with similarity scores</returns>
    [HttpGet("similarity")]
    public async Task<ActionResult<SimilaritySearchResponse>> SimilaritySearch(
        [FromQuery] string objectKey,
        [FromQuery] int limit = 20,
        [FromQuery] double threshold = 0.5,
        [FromQuery] string? projectName = null,
        [FromQuery] string? year = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new SimilaritySearchResponse { ReferenceObjectKey = objectKey };

        try
        {
            _logger.LogInformation("Similarity search request: ObjectKey='{ObjectKey}', Limit={Limit}, Threshold={Threshold}, Project={Project}, Year={Year}", 
                objectKey, limit, threshold, projectName, year);

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return BadRequest(new SimilaritySearchResponse 
                { 
                    ReferenceObjectKey = objectKey ?? "", 
                    Success = false, 
                    ErrorMessage = "Reference image object key cannot be empty" 
                });
            }

            if (limit < 1 || limit > 100)
            {
                return BadRequest(new SimilaritySearchResponse 
                { 
                    ReferenceObjectKey = objectKey, 
                    Success = false, 
                    ErrorMessage = "Limit must be between 1 and 100" 
                });
            }

            if (threshold < 0.0 || threshold > 1.0)
            {
                return BadRequest(new SimilaritySearchResponse 
                { 
                    ReferenceObjectKey = objectKey, 
                    Success = false, 
                    ErrorMessage = "Threshold must be between 0.0 and 1.0" 
                });
            }

            // Get the embedding for the reference image
            _logger.LogDebug("Retrieving embedding for reference image: {ObjectKey}", objectKey);
            var referenceEmbedding = await _vectorStore.GetEmbeddingAsync(objectKey);
            
            if (referenceEmbedding == null)
            {
                return NotFound(new SimilaritySearchResponse 
                { 
                    ReferenceObjectKey = objectKey, 
                    Success = false, 
                    ErrorMessage = "Reference image not found or embedding not available" 
                });
            }

            // Build search filters
            var filters = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                filters["project_name"] = projectName;
            }
            if (!string.IsNullOrWhiteSpace(year))
            {
                filters["year"] = year;
            }

            // Perform vector similarity search
            _logger.LogDebug("Performing similarity search with {Dimensions} dimensional reference vector", referenceEmbedding.Length);
            var vectorResults = await _vectorStore.SearchAsync(referenceEmbedding, limit + 1, threshold, filters);

            // Filter out the reference image itself and convert results
            var searchResults = vectorResults
                .Where(vr => vr.ObjectKey != objectKey) // Exclude reference image
                .Take(limit) // Limit results after excluding reference
                .Select(vr => CreateSimilaritySearchResult(vr))
                .ToList();

            // Populate response
            response.Results = searchResults;
            response.TotalResults = searchResults.Count;
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            response.Success = true;

            _logger.LogInformation("Similarity search completed: Found {ResultCount} results in {ProcessingTimeMs}ms for reference '{ObjectKey}'", 
                response.TotalResults, response.ProcessingTimeMs, objectKey);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid similarity search request: {ObjectKey}", objectKey);
            response.Success = false;
            response.ErrorMessage = ex.Message;
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing similarity search request: {ObjectKey}", objectKey);
            response.Success = false;
            response.ErrorMessage = "Internal server error occurred while processing search request";
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Advanced visual similarity search with detailed request model.
    /// </summary>
    /// <param name="request">Detailed similarity search request</param>
    /// <returns>Visual similarity search results</returns>
    [HttpPost("similarity")]
    public async Task<ActionResult<SimilaritySearchResponse>> SimilaritySearchPost([FromBody] SimilaritySearchRequest request)
    {
        // Delegate to GET method with parameters from request body
        return await SimilaritySearch(
            request.ObjectKey, 
            request.Limit, 
            request.Threshold, 
            request.ProjectName, 
            request.Year);
    }

    private static SemanticSearchResult CreateSemanticSearchResult(VectorSearchResult vectorResult)
    {
        var result = new SemanticSearchResult
        {
            ObjectKey = vectorResult.ObjectKey,
            SimilarityScore = vectorResult.SimilarityScore,
            Metadata = vectorResult.Metadata
        };

        // Extract metadata fields
        if (vectorResult.Metadata.TryGetValue("path", out var pathObj) && pathObj is string path)
        {
            result.ObjectKey = path;
            
            // Parse file information from object key path
            // Expected format: {year}/{timestamp}/{projectName}/{directoryName}/{fileName}
            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length >= 1)
            {
                result.FileName = pathParts[^1]; // Last part is filename
            }
            if (pathParts.Length >= 2)
            {
                result.DirectoryName = pathParts[^2]; // Second to last is directory
            }
            if (pathParts.Length >= 3)
            {
                result.ProjectName = pathParts[^3]; // Third to last is project
            }
            if (pathParts.Length >= 4)
            {
                result.Year = pathParts[^4]; // Fourth to last might be year or timestamp
            }
        }

        // Extract other metadata fields if available
        if (vectorResult.Metadata.TryGetValue("project_name", out var projectObj) && projectObj is string project)
        {
            result.ProjectName = project;
        }
        if (vectorResult.Metadata.TryGetValue("year", out var yearObj) && yearObj is string yearStr)
        {
            result.Year = yearStr;
        }
        if (vectorResult.Metadata.TryGetValue("upload_date", out var uploadObj) && uploadObj is string uploadStr)
        {
            if (DateTime.TryParse(uploadStr, out var uploadDate))
            {
                result.UploadDate = uploadDate;
            }
        }

        return result;
    }

    private static SimilaritySearchResult CreateSimilaritySearchResult(VectorSearchResult vectorResult)
    {
        var result = new SimilaritySearchResult
        {
            ObjectKey = vectorResult.ObjectKey,
            SimilarityScore = vectorResult.SimilarityScore,
            Metadata = vectorResult.Metadata
        };

        // Extract metadata fields
        if (vectorResult.Metadata.TryGetValue("path", out var pathObj) && pathObj is string path)
        {
            result.ObjectKey = path;
            
            // Parse file information from object key path
            // Expected format: {year}/{timestamp}/{projectName}/{directoryName}/{fileName}
            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length >= 1)
            {
                result.FileName = pathParts[^1]; // Last part is filename
            }
            if (pathParts.Length >= 2)
            {
                result.DirectoryName = pathParts[^2]; // Second to last is directory
            }
            if (pathParts.Length >= 3)
            {
                result.ProjectName = pathParts[^3]; // Third to last is project
            }
            if (pathParts.Length >= 4)
            {
                result.Year = pathParts[^4]; // Fourth to last might be year or timestamp
            }
        }

        // Extract other metadata fields if available
        if (vectorResult.Metadata.TryGetValue("project_name", out var projectObj) && projectObj is string project)
        {
            result.ProjectName = project;
        }
        if (vectorResult.Metadata.TryGetValue("year", out var yearObj) && yearObj is string yearStr)
        {
            result.Year = yearStr;
        }
        if (vectorResult.Metadata.TryGetValue("upload_date", out var uploadObj) && uploadObj is string uploadStr)
        {
            if (DateTime.TryParse(uploadStr, out var uploadDate))
            {
                result.UploadDate = uploadDate;
            }
        }

        return result;
    }

    /// <summary>
    /// Perform complex multi-criteria search combining semantic search, visual similarity, and metadata filters.
    /// </summary>
    /// <param name="request">Complex search request with multiple criteria</param>
    /// <returns>Combined search results with relevance scoring</returns>
    [HttpPost("query")]
    public async Task<ActionResult<ComplexSearchResponse>> ComplexSearch([FromBody] ComplexSearchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new ComplexSearchResponse 
        { 
            Request = request,
            Breakdown = new SearchResultBreakdown()
        };

        try
        {
            _logger.LogInformation("Complex search request: Semantic='{SemanticQuery}', Similarity='{SimilarityRef}', Mode={Mode}, Limit={Limit}", 
                request.SemanticQuery, request.SimilarityReferenceKey, request.CombinationMode, request.Limit);

            // Validate that at least one search criteria is provided
            if (string.IsNullOrWhiteSpace(request.SemanticQuery) && string.IsNullOrWhiteSpace(request.SimilarityReferenceKey))
            {
                return BadRequest(new ComplexSearchResponse 
                { 
                    Request = request,
                    Success = false, 
                    ErrorMessage = "At least one search criteria must be provided (SemanticQuery or SimilarityReferenceKey)" 
                });
            }

            // Validate weights sum to reasonable values for weighted combination
            if (request.CombinationMode == SearchCombinationMode.WeightedCombination)
            {
                var totalWeight = request.SemanticWeight + request.SimilarityWeight;
                if (totalWeight <= 0.0 || totalWeight > 2.0)
                {
                    return BadRequest(new ComplexSearchResponse 
                    { 
                        Request = request,
                        Success = false, 
                        ErrorMessage = "Combined weights must be greater than 0.0 and reasonable for weighted combination" 
                    });
                }
            }

            var allResults = new List<ComplexSearchResult>();
            var semanticResults = new List<SemanticSearchResult>();
            var similarityResults = new List<SimilaritySearchResult>();

            // Build common filters from request
            var commonFilters = BuildCommonFilters(request.Filters);

            // Perform semantic search if query provided
            if (!string.IsNullOrWhiteSpace(request.SemanticQuery))
            {
                var semanticStopwatch = Stopwatch.StartNew();
                try
                {
                    _logger.LogDebug("Performing semantic search for query: {Query}", request.SemanticQuery);
                    var queryEmbedding = await _embeddingService.GenerateTextEmbeddingAsync(request.SemanticQuery);
                    var vectorResults = await _vectorStore.SearchAsync(queryEmbedding, request.Limit * 2, request.Threshold, commonFilters);
                    
                    semanticResults = vectorResults.Select(vr => CreateSemanticSearchResult(vr)).ToList();
                    response.Breakdown.SemanticResults = semanticResults.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Semantic search failed: {Query}", request.SemanticQuery);
                }
                response.Breakdown.SemanticSearchTimeMs = semanticStopwatch.ElapsedMilliseconds;
            }

            // Perform similarity search if reference image provided
            if (!string.IsNullOrWhiteSpace(request.SimilarityReferenceKey))
            {
                var similarityStopwatch = Stopwatch.StartNew();
                try
                {
                    _logger.LogDebug("Performing similarity search for reference: {Reference}", request.SimilarityReferenceKey);
                    var referenceEmbedding = await _vectorStore.GetEmbeddingAsync(request.SimilarityReferenceKey);
                    
                    if (referenceEmbedding != null)
                    {
                        var vectorResults = await _vectorStore.SearchAsync(referenceEmbedding, request.Limit * 2, request.Threshold, commonFilters);
                        
                        // Filter out the reference image itself
                        similarityResults = vectorResults
                            .Where(vr => vr.ObjectKey != request.SimilarityReferenceKey)
                            .Select(vr => CreateSimilaritySearchResult(vr))
                            .ToList();
                        
                        response.Breakdown.SimilarityResults = similarityResults.Count;
                    }
                    else
                    {
                        _logger.LogWarning("Reference image not found for similarity search: {Reference}", request.SimilarityReferenceKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Similarity search failed: {Reference}", request.SimilarityReferenceKey);
                }
                response.Breakdown.SimilaritySearchTimeMs = similarityStopwatch.ElapsedMilliseconds;
            }

            // Combine results based on combination mode
            var combinationStopwatch = Stopwatch.StartNew();
            allResults = CombineSearchResults(semanticResults, similarityResults, request, response.Breakdown);
            response.Breakdown.CombinationTimeMs = combinationStopwatch.ElapsedMilliseconds;

            // Apply additional filters if specified
            allResults = ApplyAdditionalFilters(allResults, request.Filters);

            // Limit and populate response
            response.Results = allResults.Take(request.Limit).ToList();
            response.TotalResults = response.Results.Count;
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            response.Success = true;

            _logger.LogInformation("Complex search completed: Found {ResultCount} results in {ProcessingTimeMs}ms", 
                response.TotalResults, response.ProcessingTimeMs);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid complex search request");
            response.Success = false;
            response.ErrorMessage = ex.Message;
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing complex search request");
            response.Success = false;
            response.ErrorMessage = "Internal server error occurred while processing search request";
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return StatusCode(500, response);
        }
    }

    private Dictionary<string, object> BuildCommonFilters(SearchFilters? filters)
    {
        var commonFilters = new Dictionary<string, object>();
        
        if (filters == null) return commonFilters;

        // Add project filter (use first project if multiple specified)
        if (filters.ProjectNames?.Any() == true)
        {
            commonFilters["project_name"] = filters.ProjectNames.First();
        }

        // Add year filter (use first year if multiple specified)
        if (filters.Years?.Any() == true)
        {
            commonFilters["year"] = filters.Years.First();
        }

        // Add custom filters
        if (filters.CustomFilters?.Any() == true)
        {
            foreach (var kvp in filters.CustomFilters)
            {
                commonFilters[kvp.Key] = kvp.Value;
            }
        }

        return commonFilters;
    }

    private List<ComplexSearchResult> CombineSearchResults(
        List<SemanticSearchResult> semanticResults, 
        List<SimilaritySearchResult> similarityResults, 
        ComplexSearchRequest request,
        SearchResultBreakdown breakdown)
    {
        var combinedResults = new List<ComplexSearchResult>();
        var resultMap = new Dictionary<string, ComplexSearchResult>();

        // Add semantic results
        foreach (var semanticResult in semanticResults)
        {
            var complexResult = new ComplexSearchResult
            {
                ObjectKey = semanticResult.ObjectKey,
                FileName = semanticResult.FileName,
                ProjectName = semanticResult.ProjectName,
                DirectoryName = semanticResult.DirectoryName,
                Year = semanticResult.Year,
                UploadDate = semanticResult.UploadDate,
                ImageUrl = semanticResult.ImageUrl,
                Metadata = semanticResult.Metadata,
                SemanticScore = semanticResult.SimilarityScore,
                MatchedSearchTypes = new List<string> { "semantic" }
            };

            resultMap[semanticResult.ObjectKey] = complexResult;
        }

        // Add or merge similarity results
        foreach (var similarityResult in similarityResults)
        {
            if (resultMap.TryGetValue(similarityResult.ObjectKey, out var existingResult))
            {
                // Merge with existing semantic result
                existingResult.SimilarityScore = similarityResult.SimilarityScore;
                existingResult.MatchedSearchTypes.Add("similarity");
                breakdown.OverlapResults++;
            }
            else
            {
                // Add new similarity-only result
                var complexResult = new ComplexSearchResult
                {
                    ObjectKey = similarityResult.ObjectKey,
                    FileName = similarityResult.FileName,
                    ProjectName = similarityResult.ProjectName,
                    DirectoryName = similarityResult.DirectoryName,
                    Year = similarityResult.Year,
                    UploadDate = similarityResult.UploadDate,
                    ImageUrl = similarityResult.ImageUrl,
                    Metadata = similarityResult.Metadata,
                    SimilarityScore = similarityResult.SimilarityScore,
                    MatchedSearchTypes = new List<string> { "similarity" }
                };

                resultMap[similarityResult.ObjectKey] = complexResult;
            }
        }

        // Calculate combined relevance scores based on combination mode
        foreach (var result in resultMap.Values)
        {
            result.RelevanceScore = CalculateRelevanceScore(result, request);
        }

        // Apply combination mode filtering
        switch (request.CombinationMode)
        {
            case SearchCombinationMode.Union:
                combinedResults = resultMap.Values.ToList();
                break;
                
            case SearchCombinationMode.Intersection:
                combinedResults = resultMap.Values
                    .Where(r => r.MatchedSearchTypes.Count > 1)
                    .ToList();
                break;
                
            case SearchCombinationMode.WeightedCombination:
                combinedResults = resultMap.Values.ToList();
                break;
        }

        // Sort by relevance score
        return combinedResults.OrderByDescending(r => r.RelevanceScore).ToList();
    }

    private double CalculateRelevanceScore(ComplexSearchResult result, ComplexSearchRequest request)
    {
        double score = 0.0;
        double totalWeight = 0.0;

        if (result.SemanticScore.HasValue && !string.IsNullOrWhiteSpace(request.SemanticQuery))
        {
            score += result.SemanticScore.Value * request.SemanticWeight;
            totalWeight += request.SemanticWeight;
        }

        if (result.SimilarityScore.HasValue && !string.IsNullOrWhiteSpace(request.SimilarityReferenceKey))
        {
            score += result.SimilarityScore.Value * request.SimilarityWeight;
            totalWeight += request.SimilarityWeight;
        }

        // Normalize by total weight if using weighted combination
        if (request.CombinationMode == SearchCombinationMode.WeightedCombination && totalWeight > 0)
        {
            return score / totalWeight;
        }

        // For other modes, use the highest individual score
        var maxScore = Math.Max(result.SemanticScore ?? 0.0, result.SimilarityScore ?? 0.0);
        
        // Boost score if result appears in multiple search types
        if (result.MatchedSearchTypes.Count > 1)
        {
            maxScore *= 1.1; // 10% boost for multi-type matches
        }

        return Math.Min(maxScore, 1.0); // Cap at 1.0
    }

    private List<ComplexSearchResult> ApplyAdditionalFilters(List<ComplexSearchResult> results, SearchFilters? filters)
    {
        if (filters == null) return results;

        var filteredResults = results.AsEnumerable();

        // Filter by project names (if multiple specified)
        if (filters.ProjectNames?.Count > 1)
        {
            filteredResults = filteredResults.Where(r => 
                filters.ProjectNames.Contains(r.ProjectName ?? ""));
        }

        // Filter by years (if multiple specified)
        if (filters.Years?.Count > 1)
        {
            filteredResults = filteredResults.Where(r => 
                filters.Years.Contains(r.Year ?? ""));
        }

        // Filter by directory names
        if (filters.DirectoryNames?.Any() == true)
        {
            filteredResults = filteredResults.Where(r => 
                filters.DirectoryNames.Contains(r.DirectoryName ?? ""));
        }

        // Filter by file extensions
        if (filters.FileExtensions?.Any() == true)
        {
            filteredResults = filteredResults.Where(r => 
            {
                var extension = Path.GetExtension(r.FileName)?.TrimStart('.').ToLowerInvariant();
                return extension != null && filters.FileExtensions.Any(ext => 
                    ext.ToLowerInvariant() == extension);
            });
        }

        // Filter by upload date range
        if (filters.UploadDateRange != null)
        {
            if (filters.UploadDateRange.StartDate.HasValue)
            {
                filteredResults = filteredResults.Where(r => 
                    r.UploadDate >= filters.UploadDateRange.StartDate.Value);
            }
            
            if (filters.UploadDateRange.EndDate.HasValue)
            {
                filteredResults = filteredResults.Where(r => 
                    r.UploadDate <= filters.UploadDateRange.EndDate.Value);
            }
        }

        return filteredResults.ToList();
    }
}
