using Google.Cloud.Retail.V2;
using VertexSearchApi.DTOs.Response;

namespace VertexSearchApi.Services.Mappers;

public class AutocompleteResponseMapper
{
    public AutocompleteResponse ToApiResponse(CompleteQueryResponse retailResponse)
    {
        var suggestions = retailResponse.CompletionResults
            .Select(r => new AutocompleteSuggestion(r.Suggestion))
            .ToList();

        return new AutocompleteResponse(suggestions, retailResponse.AttributionToken);
    }
}
