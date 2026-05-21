using Google.Cloud.Retail.V2;
using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;

namespace VertexSearchApi.Services.Mappers;

public class SearchRequestMapper
{
    private readonly GcpOptions _gcp;
    private readonly SearchOptions _search;

    public SearchRequestMapper(IOptions<GcpOptions> gcp, IOptions<SearchOptions> search)
    {
        _gcp    = gcp.Value;
        _search = search.Value;
    }

    public SearchRequest ToRetailSearchRequest(KeywordSearchRequest api)
    {
        int pageSize = api.PageSize ?? _search.DefaultPageSize;

        var request = new SearchRequest
        {
            Branch             = GcpPaths.Branch(_gcp),
            Placement          = GcpPaths.Placement(_gcp),
            Query              = api.Query!,
            VisitorId          = api.VisitorId!,
            PageSize           = pageSize,
            QueryExpansionSpec = BuildQueryExpansionSpec(api.QueryExpansionCondition),
            CanonicalFilter    = api.CanonicalFilter ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(api.PageToken))
            request.PageToken = api.PageToken;
        else if (api.Offset.HasValue)
            request.Offset = api.Offset.Value;

        if (!string.IsNullOrWhiteSpace(api.Filter))
            request.Filter = api.Filter;

        if (!string.IsNullOrWhiteSpace(api.CanonicalFilter))
            request.CanonicalFilter = api.CanonicalFilter;

        if (!string.IsNullOrWhiteSpace(api.StoreId))
            request.PlaceId = api.StoreId;

        foreach (var rollupKey in api.VariantRollupKeys ?? [])
        {
            if (!string.IsNullOrWhiteSpace(rollupKey))
                request.VariantRollupKeys.Add(rollupKey);
        }
        var sort = ResolveSort(api.OrderBy);
        if (!string.IsNullOrWhiteSpace(sort))
            request.OrderBy = sort;

        foreach (var key in api.FacetKeys ?? [])
        {
            var resolved = _search.FacetKeys.TryGetValue(key, out var mapped) ? mapped : key;
            request.FacetSpecs.Add(new SearchRequest.Types.FacetSpec
            {
                FacetKey = new SearchRequest.Types.FacetSpec.Types.FacetKey { Key = resolved }
            });
        }

        return request;
    }

    private string ResolveSort(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return string.Empty;
        return _search.SortMap.TryGetValue(alias, out var mapped) ? mapped : alias;
    }

    private static SearchRequest.Types.QueryExpansionSpec BuildQueryExpansionSpec(string? condition)
    {
        var parsed = Enum.TryParse<SearchRequest.Types.QueryExpansionSpec.Types.Condition>(
            condition, ignoreCase: true, out var result)
            ? result
            : SearchRequest.Types.QueryExpansionSpec.Types.Condition.Auto;

        return new SearchRequest.Types.QueryExpansionSpec { Condition = parsed };
    }
}
