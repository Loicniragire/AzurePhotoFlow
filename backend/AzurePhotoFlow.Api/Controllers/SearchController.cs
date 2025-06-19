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

            // Perform vector similarity search
            _logger.LogDebug("Performing vector search with {Dimensions} dimensional query vector", queryEmbedding.Length);
            var vectorResults = await _vectorStore.SearchAsync(queryEmbedding, limit, threshold, filters);

            // Convert vector search results to semantic search results
            var searchResults = vectorResults.Select(vr => CreateSemanticSearchResult(vr)).ToList();

            // Populate response
            response.Results = searchResults;
            response.TotalResults = searchResults.Count;
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
}