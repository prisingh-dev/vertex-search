using Google.Cloud.Retail.V2;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Autocomplete;

public class MockAutocompleteStep : Step<AutocompleteContext>
{
    public MockAutocompleteStep(ILogger<MockAutocompleteStep> logger) : base(logger) { }

    protected override Task InnerHandleAsync(AutocompleteContext context)
    {
        context.RetailCompleteQueryResponse = BuildMockResponse(context);
        return Task.CompletedTask;
    }

    private static CompleteQueryResponse BuildMockResponse(AutocompleteContext context)
    {
        var query = context.RetailCompleteQueryRequest?.Query ?? string.Empty;
        int max   = context.RetailCompleteQueryRequest?.MaxSuggestions ?? 10;

        var matches = AllSuggestions()
            .Where(s => string.IsNullOrEmpty(query)
                     || s.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .Take(max);

        var response = new CompleteQueryResponse
        {
            AttributionToken = $"mock-autocomplete-{Guid.NewGuid():N}"
        };
        response.CompletionResults.AddRange(
            matches.Select(s => new CompleteQueryResponse.Types.CompletionResult { Suggestion = s }));

        return response;
    }

    private static IEnumerable<string> AllSuggestions() =>
    [
        "running shoes",
        "running shorts",
        "running jacket",
        "running socks",
        "running cap",
        "trail running shoes",
        "road running shoes",
        "basketball shoes",
        "basketball shorts",
        "training shoes",
        "yoga mat",
        "yoga pants",
        "gym bag",
        "gym gloves",
        "water bottle",
        "protein powder",
        "cycling shorts",
        "cycling helmet",
        "swimming goggles",
        "tennis racket"
    ];
}
