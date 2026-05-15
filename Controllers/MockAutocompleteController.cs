using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.DTOs.Response;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Controllers;

/// <summary>
/// Runs the full autocomplete pipeline — validation, request mapping, response mapping —
/// but replaces the GCP call with hardcoded fixture data (MockAutocompleteStep).
/// No Google Cloud credentials required.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitOptions.Policies.Mock)]
public class MockAutocompleteController : ControllerBase
{
    private readonly StepService<AutocompleteContext> _mockStepService;
    private readonly ILogger<MockAutocompleteController> _logger;

    public MockAutocompleteController(
        [FromKeyedServices("mock")] StepService<AutocompleteContext> mockStepService,
        ILogger<MockAutocompleteController> logger)
    {
        _mockStepService = mockStepService;
        _logger          = logger;
    }

    /// <summary>
    /// Mock autocomplete — same validation and schema as GET /api/v1/autocomplete,
    /// but returns fixture suggestions instead of calling Vertex AI.
    /// Suggestions are prefix-filtered by the query string.
    /// </summary>
    /// <param name="query">Partial search term.</param>
    /// <param name="visitorId">Session or user identifier.</param>
    /// <param name="maxSuggestions">Optional cap on suggestions (default 10, max 20).</param>
    [HttpGet("autocomplete/mock")]
    [ProducesResponseType(typeof(AutocompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<AutocompleteResponse> AutocompleteMock(
        [FromQuery] string? query,
        [FromQuery] string? visitorId,
        [FromQuery] int? maxSuggestions)
    {
        _logger.LogInformation(
            "Mock autocomplete request: query='{Query}', visitorId='{VisitorId}'",
            query, visitorId);

        var context = new AutocompleteContext
        {
            ApiRequest = new AutocompleteRequest
            {
                Query          = query,
                VisitorId      = visitorId,
                MaxSuggestions = maxSuggestions
            }
        };

        return (await _mockStepService.ExecuteAsync(context)).ApiResponse!;
    }
}
