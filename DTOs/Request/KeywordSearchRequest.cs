namespace VertexSearchApi.DTOs.Request;

public class KeywordSearchRequest
{
    public string? Query { get; set; }
    public string? VisitorId { get; set; }
    public int? PageSize { get; set; }
    public int? Offset { get; set; }
    public string? OrderBy { get; set; }
    public string? Filter { get; set; }
    public List<string>? FacetKeys { get; set; }
    public string? QueryExpansionCondition { get; set; }
}
