namespace VertexSearchApi.DTOs.Response;

public class KeywordSearchResponse
{
    public List<ProductResult>? Products { get; set; }
    public List<FacetResult>? Facets { get; set; }
    public SearchStats? Stats { get; set; }
    public string? CorrectedQuery { get; set; }
    public string? AttributionToken { get; set; }
    public string? RedirectUri { get; set; }
    public List<string>? AppliedControls { get; set; }
}
