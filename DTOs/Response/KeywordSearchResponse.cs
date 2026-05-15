namespace VertexSearchApi.DTOs.Response;

public record KeywordSearchResponse
{
    public List<ProductResult>? Products { get; init; }
    public List<FacetResult>? Facets { get; init; }
    public SearchStats? Stats { get; init; }
    public string? NextPageToken { get; init; }
    public string? CorrectedQuery { get; init; }
    public string? AttributionToken { get; init; }
    public string? RedirectUri { get; init; }
    public List<string>? AppliedControls { get; init; }
}
