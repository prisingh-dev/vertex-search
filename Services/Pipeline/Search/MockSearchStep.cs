using Google.Cloud.Retail.V2;
using Google.Protobuf.WellKnownTypes;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Search;

public class MockSearchStep : Step<SearchContext>
{
    public MockSearchStep(ILogger<MockSearchStep> logger) : base(logger) { }

    protected override Task InnerHandleAsync(SearchContext context)
    {
        context.RetailSearchResponse = BuildMockResponse(context);
        return Task.CompletedTask;
    }

    private static SearchResponse BuildMockResponse(SearchContext context)
    {
        int pageSize = context.RetailSearchRequest?.PageSize ?? 20;
        int offset   = context.RetailSearchRequest?.Offset   ?? 0;
        var query    = context.RetailSearchRequest?.Query    ?? string.Empty;

        var allResults = AllResults();
        var response   = new SearchResponse { TotalSize = allResults.Count };

        response.Results.AddRange(allResults.Skip(offset).Take(pageSize));
        response.AppliedControls.AddRange(["boost-new-arrivals", "pin-sale-items"]);
        response.AttributionToken = $"mock-token-{Guid.NewGuid():N}";

        // Demo spell-correction: trigger by including "shoez" in the query
        if (query.Contains("shoez", StringComparison.OrdinalIgnoreCase))
            response.CorrectedQuery = query.Replace(
                "shoez", "shoes", StringComparison.OrdinalIgnoreCase);

        // Return only the facets that were explicitly requested; default to all
        var requestedKeys = context.RetailSearchRequest?.FacetSpecs
            .Select(f => f.FacetKey.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        foreach (var facet in AllFacets())
        {
            if (requestedKeys.Count == 0 || requestedKeys.Contains(facet.Key))
                response.Facets.Add(facet);
        }

        return response;
    }

    // ── Fixture data ─────────────────────────────────────────────────────────

    private static List<SearchResponse.Types.SearchResult> AllResults() =>
    [
        MakeResult(
            id: "product-001",
            title: "Men's Trail Running Shoes — Red",
            categories: ["Footwear", "Footwear > Running", "Footwear > Running > Trail"],
            uri: "https://example.com/products/001",
            attrs: new() { ["color"] = Text("Red"), ["brand"] = Text("Nike"),
                           ["gender"] = Text("Men"), ["material"] = Text("Mesh") },
            variantIds: ["product-001-sz9", "product-001-sz10", "product-001-sz11"],
            price: 189.99f, originalPrice: 229.99f, cost: 95.00f,
            priceEffectiveTime: new DateTime(2025, 1,  1,  0,  0,  0, DateTimeKind.Utc),
            priceExpireTime:    new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            priceRangeCurrent:  (169.99, 209.99),
            priceRangeOriginal: (209.99, 249.99)),

        MakeResult(
            id: "product-002",
            title: "Women's Lightweight Running Shoes — Blue",
            categories: ["Footwear", "Footwear > Running"],
            uri: "https://example.com/products/002",
            attrs: new() { ["color"] = Text("Blue"), ["brand"] = Text("Adidas"),
                           ["gender"] = Text("Women") },
            variantIds: ["product-002-sz7", "product-002-sz8"],
            price: 149.99f, originalPrice: 179.99f, cost: 75.00f,
            priceEffectiveTime: new DateTime(2025, 3,  1,  0,  0,  0, DateTimeKind.Utc),
            priceExpireTime:    new DateTime(2025, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            priceRangeCurrent:  (139.99, 159.99),
            priceRangeOriginal: (169.99, 189.99)),

        MakeResult(
            id: "product-003",
            title: "Unisex Road Running Shoes — Black",
            categories: ["Footwear", "Footwear > Running", "Footwear > Running > Road"],
            uri: "https://example.com/products/003",
            attrs: new() { ["color"] = Text("Black"), ["brand"] = Text("New Balance"),
                           ["gender"] = Text("Unisex") },
            variantIds: ["product-003-sz8", "product-003-sz9", "product-003-sz10", "product-003-sz11"],
            price: 169.99f, cost: 85.00f,
            priceRangeCurrent: (159.99, 179.99)),

        MakeResult(
            id: "product-004",
            title: "Kids Running Shoes — Green",
            categories: ["Footwear", "Footwear > Running", "Footwear > Kids"],
            uri: "https://example.com/products/004",
            attrs: new() { ["color"] = Text("Green"), ["brand"] = Text("Puma"),
                           ["gender"] = Text("Kids") },
            variantIds: ["product-004-sz4", "product-004-sz5"],
            price: 79.99f, originalPrice: 99.99f, cost: 40.00f,
            priceEffectiveTime: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            priceRangeCurrent:  (74.99, 84.99),
            priceRangeOriginal: (94.99, 104.99)),

        MakeResult(
            id: "product-005",
            title: "Pro Marathon Running Shoes — White",
            categories: ["Footwear", "Footwear > Running", "Footwear > Running > Marathon"],
            uri: "https://example.com/products/005",
            attrs: new() { ["color"] = Text("White"), ["brand"] = Text("Asics"),
                           ["gender"] = Text("Unisex") },
            variantIds: ["product-005-sz9", "product-005-sz10"],
            price: 249.99f, cost: 125.00f,
            priceRangeCurrent: (239.99, 259.99))
    ];

    private static List<SearchResponse.Types.Facet> AllFacets() =>
    [
        MakeFacet("colorFamilies",
            [("Red",42), ("Blue",35), ("Black",28), ("White",19), ("Green",11)]),
        MakeFacet("brands",
            [("Nike",58), ("Adidas",47), ("New Balance",32), ("Asics",24), ("Puma",18)]),
        MakeFacet("sizes",
            [("7",22), ("8",31), ("9",45), ("10",48), ("11",39), ("12",21)]),
        MakeFacet("categories",
        [
            ("Footwear > Running",           135),
            ("Footwear > Running > Trail",    42),
            ("Footwear > Running > Road",     38),
            ("Footwear > Running > Marathon", 19),
            ("Footwear > Kids",               11)
        ])
    ];

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SearchResponse.Types.SearchResult MakeResult(
        string id, string title,
        IEnumerable<string> categories,
        string uri,
        Dictionary<string, CustomAttribute> attrs,
        IEnumerable<string> variantIds,
        float price,
        float? originalPrice = null,
        float? cost = null,
        DateTime? priceEffectiveTime = null,
        DateTime? priceExpireTime = null,
        (double min, double max)? priceRangeCurrent = null,
        (double min, double max)? priceRangeOriginal = null)
    {
        var priceInfo = new PriceInfo
        {
            CurrencyCode  = "AUD",
            Price         = price,
            OriginalPrice = originalPrice ?? price,
            Cost          = cost ?? 0f
        };

        if (priceEffectiveTime.HasValue)
            priceInfo.PriceEffectiveTime = Timestamp.FromDateTime(priceEffectiveTime.Value);
        if (priceExpireTime.HasValue)
            priceInfo.PriceExpireTime = Timestamp.FromDateTime(priceExpireTime.Value);
        if (priceRangeCurrent.HasValue || priceRangeOriginal.HasValue)
            priceInfo.PriceRange = MakePriceRange(priceRangeCurrent, priceRangeOriginal);

        var product = new Product
        {
            Id = id, PrimaryProductId = id, Title = title, Uri = uri,
            PriceInfo = priceInfo
        };
        product.Categories.AddRange(categories);
        product.Variants.AddRange(variantIds.Select(vid => new Product { Id = vid }));
        foreach (var (k, v) in attrs) product.Attributes[k] = v;

        return new SearchResponse.Types.SearchResult { Id = id, Product = product };
    }

    private static PriceInfo.Types.PriceRange MakePriceRange(
        (double min, double max)? current,
        (double min, double max)? original)
    {
        var range = new PriceInfo.Types.PriceRange();
        if (current.HasValue)
            range.Price = new Interval { Minimum = current.Value.min, Maximum = current.Value.max };
        if (original.HasValue)
            range.OriginalPrice = new Interval { Minimum = original.Value.min, Maximum = original.Value.max };
        return range;
    }

    private static SearchResponse.Types.Facet MakeFacet(
        string key, IEnumerable<(string value, long count)> values)
    {
        var facet = new SearchResponse.Types.Facet { Key = key };
        facet.Values.AddRange(values.Select(v =>
            new SearchResponse.Types.Facet.Types.FacetValue { Value = v.value, Count = v.count }));
        return facet;
    }

    private static CustomAttribute Text(params string[] values)
    {
        var attr = new CustomAttribute();
        attr.Text.AddRange(values);
        return attr;
    }
}
