namespace VertexSearchApi.DTOs.Response;

public record AutocompleteResponse(
    List<AutocompleteSuggestion> Suggestions,
    string? AttributionToken
);
