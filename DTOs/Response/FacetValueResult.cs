namespace VertexSearchApi.DTOs.Response;

public class FacetValueResult
{
    public string? Value { get; set; }
    public long Count { get; set; }

    public FacetValueResult(string value, long count)
    {
        Value = value;
        Count = count;
    }
}
