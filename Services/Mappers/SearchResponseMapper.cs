using Google.Cloud.Retail.V2;
using Google.Protobuf.WellKnownTypes;
using VertexSearchApi.DTOs.Response;
using VertexSearchApi.Services.Context;

namespace VertexSearchApi.Services.Mappers;

public class SearchResponseMapper
{
    public KeywordSearchResponse ToApiResponse(SearchContext context)
    {
        var response       = context.RetailSearchResponse!;
        int resolvedOffset = context.RetailSearchRequest?.Offset ?? 0;
        string? storeId    = NullIfEmpty(context.ApiRequest?.StoreId);

        var products = response.Results
            .Where(r => r is not null)
            .Select(r => MapProduct(r, storeId))
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToList();

        var facets = response.Facets
            .Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Key))
            .Select(MapFacet)
            .Where(f => f.Values?.Count > 0)
            .ToList();

        return new KeywordSearchResponse
        {
            Products         = products.Count > 0 ? products : null,
            Facets           = facets.Count > 0 ? facets : null,
            Stats            = new SearchStats(products.Count, response.TotalSize, resolvedOffset),
            NextPageToken    = NullIfEmpty(response.NextPageToken),
            CorrectedQuery   = NullIfEmpty(response.CorrectedQuery),
            AttributionToken = NullIfEmpty(response.AttributionToken),
            RedirectUri      = NullIfEmpty(response.RedirectUri),
            AppliedControls  = response.AppliedControls.Count > 0
                ? [.. response.AppliedControls] : null
        };
    }

    private static ProductResult MapProduct(SearchResponse.Types.SearchResult result, string? storeId)
    {
        var product = result.Product;

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

        var catalogPrice = MapCatalogPrice(product.PriceInfo);
        ProductPrice? price = OverlayLocalInventoryPrice(catalogPrice, result, storeId)
                              ?? catalogPrice;

        return new ProductResult
        {
            Id         = id,
            Title      = NullIfEmpty(product.Title),
            Categories = product.Categories.Count > 0 ? [.. product.Categories] : null,
            Uri        = NullIfEmpty(product.Uri),
            Attributes = attributes.Count > 0 ? attributes : null,
            Variants   = variants.Count > 0 ? variants : null,
            Price      = price
        };
    }

 
    private static ProductPrice? TryExtractLocalInventoryPrice(
        SearchResponse.Types.SearchResult result, string? storeId)
    {
        if (storeId is null || result.VariantRollupValues.Count == 0)
            return null;

        var rollupKey = $"inventory({storeId}, price)";
        if (!result.VariantRollupValues.TryGetValue(rollupKey, out var rollupValue))
            return null;

        float? localPrice = rollupValue.KindCase switch
        {
            Value.KindOneofCase.NumberValue => (float)rollupValue.NumberValue,
            Value.KindOneofCase.ListValue   => ExtractFirstNumber(rollupValue.ListValue),
            _                               => null
        };

        if (localPrice is null or <= 0)
            return null;

        return new ProductPrice
        {
            Price = localPrice.Value
        };
    }

    private static float? ExtractFirstNumber(ListValue? list)
    {
        if (list is null) return null;
        foreach (var v in list.Values)
        {
            if (v.KindCase == Value.KindOneofCase.NumberValue && v.NumberValue > 0)
                return (float)v.NumberValue;
        }
        return null;
    }

    private static ProductPrice? MapCatalogPrice(PriceInfo? pi)
    {
        if (pi is not { Price: > 0 }) return null;
        return new ProductPrice
        {
            CurrencyCode       = NullIfEmpty(pi.CurrencyCode),
            Price              = pi.Price,
            OriginalPrice      = pi.OriginalPrice > 0 && pi.OriginalPrice != pi.Price
                                     ? pi.OriginalPrice : null,
            Cost               = pi.Cost > 0 ? pi.Cost : null,
            PriceEffectiveTime = pi.PriceEffectiveTime is { Seconds: > 0 } pet
                                     ? new DateTimeOffset(pet.ToDateTime()) : null,
            PriceExpireTime    = pi.PriceExpireTime is { Seconds: > 0 } pxt
                                     ? new DateTimeOffset(pxt.ToDateTime()) : null,
            PriceRange         = MapPriceRange(pi.PriceRange)
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

    private static ProductPriceRange? MapPriceRange(PriceInfo.Types.PriceRange? range)
    {
        if (range is null) return null;
        var pb = range.Price;
        var ob = range.OriginalPrice;
        if (pb is null && ob is null) return null;
        return new ProductPriceRange(pb?.Minimum, pb?.Maximum, ob?.Minimum, ob?.Maximum);
    }

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrEmpty(s) ? null : s;
}
