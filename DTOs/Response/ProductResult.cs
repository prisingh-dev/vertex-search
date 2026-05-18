namespace VertexSearchApi.DTOs.Response;

public record ProductResult
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public List<string>? Categories { get; init; }
    public string? Uri { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }
    public List<VariantResult>? Variants { get; init; }
    public ProductPrice? Price { get; init; }
    public Dictionary<string, object?>? VariantRollupValues { get; init; }
}
