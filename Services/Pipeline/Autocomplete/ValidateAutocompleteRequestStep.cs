using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.Exceptions;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Autocomplete;

public class ValidateAutocompleteRequestStep : Step<AutocompleteContext>
{
    private readonly AutocompleteOptions _opts;

    public ValidateAutocompleteRequestStep(
        ILogger<ValidateAutocompleteRequestStep> logger,
        IOptions<AutocompleteOptions> opts)
        : base(logger) => _opts = opts.Value;

    protected override Task InnerHandleAsync(AutocompleteContext context)
    {
        var req = context.ApiRequest
            ?? throw new InvalidSearchRequestException("Request is required");

        if (string.IsNullOrWhiteSpace(req.Query))
            throw new InvalidSearchRequestException("query is required");

        if (string.IsNullOrWhiteSpace(req.VisitorId))
            throw new InvalidSearchRequestException("visitorId is required");

        if (req.MaxSuggestions.HasValue)
        {
            if (req.MaxSuggestions < 1)
                throw new InvalidSearchRequestException("maxSuggestions must be >= 1");
            if (req.MaxSuggestions > _opts.MaxAllowedSuggestions)
                throw new InvalidSearchRequestException(
                    $"maxSuggestions must be <= {_opts.MaxAllowedSuggestions}");
        }

        return Task.CompletedTask;
    }
}
