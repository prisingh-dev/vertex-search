using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.DTOs.Response;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Controllers;

/// <summary>Exposes the Vertex AI Retail autocomplete (typeahead) endpoint.</summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitOptions.Policies.Autocomplete)]
public class AutocompleteController : ControllerBase
{
    private readonly StepService<AutocompleteContext> _stepService;
    private readonly ILogger<AutocompleteController> _logger;

    public AutocompleteController(
        StepService<AutocompleteContext> stepService,
        ILogger<AutocompleteController> logger)
    {
        _stepService = stepService;
        _logger      = logger;
    }

    /// <summary>
    /// Returns typeahead suggestions from the Vertex AI Retail completion service.
    /// </summary>
    /// <param name="query">Partial search term (lowercased before sending to GCP).</param>
    /// <param name="visitorId">Session or user identifier.</param>
    /// <param name="maxSuggestions">Optional cap on suggestions (default 10, max 20).</param>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(AutocompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<AutocompleteResponse> Autocomplete(
        [FromQuery] string? query,
        [FromQuery] string? visitorId,
        [FromQuery] int? maxSuggestions)
    {
        _logger.LogInformation(
            "Autocomplete request: query='{Query}', visitorId='{VisitorId}'",
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

        return (await _stepService.ExecuteAsync(context)).ApiResponse!;
    }
}
