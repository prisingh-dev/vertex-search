using Google.Cloud.Retail.V2;
using VertexSearchApi.Exceptions;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Search;

public class SearchStep : Step<SearchContext>
{
    private readonly SearchServiceClient _client;

    public SearchStep(ILogger<SearchStep> logger, SearchServiceClient client)
        : base(logger) => _client = client;

    protected override void InnerHandle(SearchContext context)
    {
        try
        {
            context.RetailSearchResponse = _client
                .Search(context.RetailSearchRequest!)
                .AsRawResponses()
                .First();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vertex AI Retail search failed");
            throw new SearchServiceException(ex.Message, ex);
        }
    }
}
