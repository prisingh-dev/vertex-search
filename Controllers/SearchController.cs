using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.DTOs.Response;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Controllers;

/// <summary>Exposes the Vertex AI Retail Search endpoint.</summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitOptions.Policies.Search)]
public class SearchController : ControllerBase
{
    private readonly StepService<SearchContext> _stepService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        StepService<SearchContext> stepService,
        ILogger<SearchController> logger)
    {
        _stepService = stepService;
        _logger      = logger;
    }

    /// <summary>
    /// Executes a keyword search against the Vertex AI Retail catalog.
    /// </summary>
    /// <param name="request">Search parameters.</param>
    /// <returns>Products, facets, stats, and metadata.</returns>
    [HttpPost("search")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(KeywordSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<KeywordSearchResponse> Search([FromBody] KeywordSearchRequest request)
    {
        _logger.LogInformation(
            "Keyword search: query='{Query}', visitorId='{VisitorId}'",
            request.Query, request.VisitorId);

        var context = new SearchContext { ApiRequest = request };
        return (await _stepService.ExecuteAsync(context)).ApiResponse!;
    }
}
