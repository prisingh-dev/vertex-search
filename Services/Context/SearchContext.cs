using Google.Cloud.Retail.V2;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.DTOs.Response;

namespace VertexSearchApi.Services.Context;

/// <summary>Mutable context object passed through the search pipeline steps.</summary>
public class SearchContext
{
    public KeywordSearchRequest? ApiRequest { get; set; }
    public SearchRequest? RetailSearchRequest { get; set; }
    public SearchResponse? RetailSearchResponse { get; set; }
    public KeywordSearchResponse? ApiResponse { get; set; }
}
