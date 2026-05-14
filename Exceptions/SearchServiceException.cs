namespace VertexSearchApi.Exceptions;

public class SearchServiceException : Exception
{
    public SearchServiceException(string message, Exception? inner = null)
        : base(message, inner) { }
}
