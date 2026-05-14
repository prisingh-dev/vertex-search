namespace VertexSearchApi.Config;

public class GcpOptions
{
    public const string SectionName = "Gcp";

    public string ProjectId { get; set; } = string.Empty;
    public RetailOptions Retail { get; set; } = new();

    public class RetailOptions
    {
        public string Location { get; set; } = "global";
        public string Catalogs { get; set; } = "default_catalog";
        public string Branch { get; set; } = "default_branch";
        public string Placement { get; set; } = "default_search";
    }
}
