namespace VertexSearchApi.Config;

public class AutocompleteOptions
{
    public const string SectionName = "Autocomplete";

    public int DefaultMaxSuggestions { get; set; } = 10;
    public int MaxAllowedSuggestions { get; set; } = 20;
    public string Dataset { get; set; } = "cloud-retail";
}
