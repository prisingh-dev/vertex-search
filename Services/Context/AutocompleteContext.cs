using Google.Cloud.Retail.V2;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.DTOs.Response;

namespace VertexSearchApi.Services.Context;

public class AutocompleteContext
{
    public AutocompleteRequest? ApiRequest { get; set; }
    public CompleteQueryRequest? RetailCompleteQueryRequest { get; set; }
    public CompleteQueryResponse? RetailCompleteQueryResponse { get; set; }
    public AutocompleteResponse? ApiResponse { get; set; }
}
