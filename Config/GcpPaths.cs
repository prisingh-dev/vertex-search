namespace VertexSearchApi.Config;

public static class GcpPaths
{
    public static string Branch(GcpOptions opts) =>
        $"projects/{opts.ProjectId}/locations/{opts.Retail.Location}" +
        $"/catalogs/{opts.Retail.Catalogs}/branches/{opts.Retail.Branch}";

    public static string Placement(GcpOptions opts) =>
        $"projects/{opts.ProjectId}/locations/{opts.Retail.Location}" +
        $"/catalogs/{opts.Retail.Catalogs}/servingConfigs/{opts.Retail.Placement}";

    public static string Catalog(GcpOptions opts) =>
        $"projects/{opts.ProjectId}/locations/{opts.Retail.Location}" +
        $"/catalogs/{opts.Retail.Catalogs}";
}
