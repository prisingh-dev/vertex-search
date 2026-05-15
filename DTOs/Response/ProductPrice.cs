namespace VertexSearchApi.DTOs.Response;

public record ProductPrice
{
    public string?            CurrencyCode       { get; init; }
    public float              Price              { get; init; }
    public float?             OriginalPrice      { get; init; }
    public float?             Cost               { get; init; }
    public DateTimeOffset?    PriceEffectiveTime { get; init; }
    public DateTimeOffset?    PriceExpireTime    { get; init; }
    public ProductPriceRange? PriceRange         { get; init; }
}

public record ProductPriceRange(
    double? PriceMin,
    double? PriceMax,
    double? OriginalPriceMin,
    double? OriginalPriceMax);
