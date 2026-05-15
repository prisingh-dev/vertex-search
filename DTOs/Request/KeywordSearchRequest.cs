namespace VertexSearchApi.DTOs.Request;

public record KeywordSearchRequest
{
    public string? Query { get; init; }
    public string? VisitorId { get; init; }
    public int? PageSize { get; init; }
    public int? Offset { get; init; }
    public string? PageToken { get; init; }
    public string? OrderBy { get; init; }
    public string? Filter { get; init; }
    public List<string>? FacetKeys { get; init; }
    public string? QueryExpansionCondition { get; init; }
    public string? StoreId { get; init; }
}
