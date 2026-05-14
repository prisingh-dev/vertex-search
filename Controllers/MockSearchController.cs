using Microsoft.AspNetCore.Mvc;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.DTOs.Response;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Controllers;

/// <summary>
/// Runs the full search pipeline — validation, request mapping, response mapping —
/// but replaces the GCP call with hardcoded fixture data (MockSearchStep).
/// No Google Cloud credentials required.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class MockSearchController : ControllerBase
{
    private readonly StepService<SearchContext> _mockStepService;
    private readonly ILogger<MockSearchController> _logger;

    public MockSearchController(
        [FromKeyedServices("mock")] StepService<SearchContext> mockStepService,
        ILogger<MockSearchController> logger)
    {
        _mockStepService = mockStepService;
        _logger          = logger;
    }

    /// <summary>
    /// Mock search — same validation and schema as POST /api/v1/search,
    /// but returns dummy products/facets instead of calling Vertex AI.
    /// </summary>
    /// <param name="request">Search parameters (all validations enforced).</param>
    /// <returns>Dummy products, facets, stats and metadata.</returns>
    [HttpPost("search/mock")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(KeywordSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public KeywordSearchResponse SearchMock([FromBody] KeywordSearchRequest request)
    {
        _logger.LogInformation(
            "Mock keyword search: query='{Query}', visitorId='{VisitorId}'",
            request.Query, request.VisitorId);

        var context = new SearchContext { ApiRequest = request };
        return _mockStepService.Execute(context).ApiResponse!;
    }
}
