namespace VertexSearchApi.Config;

public class SearchOptions
{
    public const string SectionName = "Search";

    public int DefaultPageSize { get; set; } = 20;
    public int MaxPageSize { get; set; } = 100;
    public Dictionary<string, string> SortMap { get; set; } = new();
    public Dictionary<string, string> FacetKeys { get; set; } = new();
}
