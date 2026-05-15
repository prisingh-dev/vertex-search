namespace VertexSearchApi.Exceptions;

public class AutocompleteServiceException : Exception
{
    public AutocompleteServiceException(string message, Exception? inner = null)
        : base(message, inner) { }
}
