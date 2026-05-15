namespace VertexSearchApi.DTOs.Request;

public record AutocompleteRequest
{
    public string? Query { get; init; }
    public string? VisitorId { get; init; }
    public int? MaxSuggestions { get; init; }
}
