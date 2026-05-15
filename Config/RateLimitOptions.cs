namespace VertexSearchApi.Config;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public static class Policies
    {
        public const string Search      = "search";
        public const string Autocomplete = "autocomplete";
        public const string Mock        = "mock";
    }

    public PolicyOptions Search      { get; set; } = new();
    public PolicyOptions Autocomplete { get; set; } = new();
    public PolicyOptions Mock        { get; set; } = new();

    public class PolicyOptions
    {
        public int PermitLimit    { get; set; } = 60;
        public int WindowSeconds  { get; set; } = 60;
        public int QueueLimit     { get; set; } = 0;
    }
}
