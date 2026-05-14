namespace VertexSearchApi.DTOs.Response;

public class VariantResult
{
    public string? Id { get; set; }

    public VariantResult(string id) => Id = id;
}
