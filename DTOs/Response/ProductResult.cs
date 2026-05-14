namespace VertexSearchApi.DTOs.Response;

public class ProductResult
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public List<string>? Categories { get; set; }
    public string? Uri { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public List<VariantResult>? Variants { get; set; }
}
