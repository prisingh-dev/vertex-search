namespace VertexSearchApi.DTOs.Response;

public record FacetResult(string? Key, List<FacetValueResult>? Values);
