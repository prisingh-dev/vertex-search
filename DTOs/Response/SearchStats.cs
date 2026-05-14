namespace VertexSearchApi.DTOs.Response;

public class SearchStats
{
    public int Returned { get; set; }
    public int TotalResults { get; set; }
    public int Offset { get; set; }

    public SearchStats(int returned, int totalResults, int offset)
    {
        Returned = returned;
        TotalResults = totalResults;
        Offset = offset;
    }
}
