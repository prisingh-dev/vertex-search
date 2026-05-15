using Google.Cloud.Retail.V2;
using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;

namespace VertexSearchApi.Services.Mappers;

public class AutocompleteRequestMapper
{
    private readonly GcpOptions _gcp;
    private readonly AutocompleteOptions _autocomplete;

    public AutocompleteRequestMapper(IOptions<GcpOptions> gcp, IOptions<AutocompleteOptions> autocomplete)
    {
        _gcp          = gcp.Value;
        _autocomplete = autocomplete.Value;
    }

    public CompleteQueryRequest ToRetailCompleteQueryRequest(AutocompleteRequest apiRequest)
    {
        int maxSuggestions = apiRequest.MaxSuggestions ?? _autocomplete.DefaultMaxSuggestions;

        var request = new CompleteQueryRequest
        {
            Catalog        = GcpPaths.Catalog(_gcp),
            Query          = apiRequest.Query!.ToLowerInvariant(),
            VisitorId      = apiRequest.VisitorId!,
            MaxSuggestions = maxSuggestions
        };

        if (!string.IsNullOrWhiteSpace(_autocomplete.Dataset))
            request.Dataset = _autocomplete.Dataset;

        return request;
    }
}
