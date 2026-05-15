using Google.Cloud.Retail.V2;
using VertexSearchApi.Exceptions;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Autocomplete;

public class AutocompleteStep : Step<AutocompleteContext>
{
    private readonly CompletionServiceClient _client;

    public AutocompleteStep(ILogger<AutocompleteStep> logger, CompletionServiceClient client)
        : base(logger) => _client = client;

    protected override async Task InnerHandleAsync(AutocompleteContext context)
    {
        try
        {
            context.RetailCompleteQueryResponse =
                await _client.CompleteQueryAsync(context.RetailCompleteQueryRequest!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vertex AI Retail autocomplete failed");
            throw new AutocompleteServiceException("Failed to fetch autocomplete suggestions", ex);
        }
    }
}
