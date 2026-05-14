namespace VertexSearchApi.Exceptions;

public class InvalidSearchRequestException : Exception
{
    public InvalidSearchRequestException(string message) : base(message) { }
}
