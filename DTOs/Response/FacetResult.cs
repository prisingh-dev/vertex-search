namespace VertexSearchApi.DTOs.Response;

public class FacetResult
{
    public string? Key { get; set; }
    public List<FacetValueResult>? Values { get; set; }

    public FacetResult(string key, List<FacetValueResult> values)
    {
        Key = key;
        Values = values;
    }
}
