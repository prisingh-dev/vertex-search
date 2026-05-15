using Google.Cloud.Retail.V2;
using VertexSearchApi.Services.Mappers;

namespace VertexSearchApi.Tests.Mappers;

public class AutocompleteResponseMapperTests
{
    private readonly AutocompleteResponseMapper _mapper = new();

    [Fact]
    public void Maps_Suggestions_In_Order()
    {
        var response = new CompleteQueryResponse { AttributionToken = "tok" };
        response.CompletionResults.AddRange([
            new CompleteQueryResponse.Types.CompletionResult { Suggestion = "shoes" },
            new CompleteQueryResponse.Types.CompletionResult { Suggestion = "shoe rack" }
        ]);

        var result = _mapper.ToApiResponse(response);

        Assert.Equal(2, result.Suggestions.Count);
        Assert.Equal("shoes",     result.Suggestions[0].Suggestion);
        Assert.Equal("shoe rack", result.Suggestions[1].Suggestion);
    }

    [Fact]
    public void Maps_AttributionToken()
    {
        var response = new CompleteQueryResponse { AttributionToken = "token-xyz" };

        var result = _mapper.ToApiResponse(response);

        Assert.Equal("token-xyz", result.AttributionToken);
    }

    [Fact]
    public void Returns_Empty_List_When_No_Suggestions()
    {
        var result = _mapper.ToApiResponse(new CompleteQueryResponse());

        Assert.Empty(result.Suggestions);
    }
}
