using Google.Cloud.Retail.V2;
using VertexSearchApi.DTOs.Response;
using VertexSearchApi.Services.Context;

namespace VertexSearchApi.Services.Mappers;

public class SearchResponseMapper
{
    public KeywordSearchResponse ToApiResponse(SearchContext context)
    {
        var response       = context.RetailSearchResponse!;
        int resolvedOffset = context.RetailSearchRequest?.Offset ?? 0;

        var products = response.Results
            .Where(r => r is not null)
            .Select(MapProduct)
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToList();

        var facets = response.Facets
            .Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Key))
            .Select(MapFacet)
            .Where(f => f.Values?.Count > 0)
            .ToList();

        return new KeywordSearchResponse
        {
            Products       = products.Count > 0 ? products : null,
            Facets         = facets.Count > 0 ? facets : null,
            Stats          = new SearchStats(products.Count, response.TotalSize, resolvedOffset),
            CorrectedQuery = NullIfEmpty(response.CorrectedQuery),
            AttributionToken = NullIfEmpty(response.AttributionToken),
            RedirectUri    = NullIfEmpty(response.RedirectUri),
            AppliedControls = response.AppliedControls.Count > 0
                ? [.. response.AppliedControls] : null
        };
    }

    private static ProductResult MapProduct(SearchResponse.Types.SearchResult result)
    {
        var product = result.Product;

        // Resolve the best available product ID (mirrors Java logic)
        string id = !string.IsNullOrWhiteSpace(product.PrimaryProductId)
            ? product.PrimaryProductId
            : !string.IsNullOrWhiteSpace(product.Id)
                ? product.Id
                : result.Id;

        var attributes = new Dictionary<string, string>();
        foreach (var (key, attr) in product.Attributes)
        {
            if (attr.Text.Count > 0)
                attributes[key] = string.Join(", ", attr.Text);
            else if (attr.Numbers.Count > 0)
                attributes[key] = string.Join(", ", attr.Numbers);
        }

        var variants = product.Variants
            .Select(v => v.Id)
            .Where(vid => !string.IsNullOrWhiteSpace(vid))
            .Distinct()
            .Select(vid => new VariantResult(vid))
            .ToList();

        return new ProductResult
        {
            Id         = id,
            Title      = NullIfEmpty(product.Title),
            Categories = product.Categories.Count > 0 ? [.. product.Categories] : null,
            Uri        = NullIfEmpty(product.Uri),
            Attributes = attributes.Count > 0 ? attributes : null,
            Variants   = variants.Count > 0 ? variants : null
        };
    }

    private static FacetResult MapFacet(SearchResponse.Types.Facet facet)
    {
        var values = facet.Values
            .Where(v => v is not null && !string.IsNullOrWhiteSpace(v.Value))
            .Select(v => new FacetValueResult(v.Value, v.Count))
            .ToList();

        return new FacetResult(facet.Key, values);
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrEmpty(s) ? null : s;
}
