using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Autocomplete;

public class ConvertAutocompleteResponseStep : Step<AutocompleteContext>
{
    private readonly AutocompleteResponseMapper _mapper;

    public ConvertAutocompleteResponseStep(
        ILogger<ConvertAutocompleteResponseStep> logger,
        AutocompleteResponseMapper mapper)
        : base(logger) => _mapper = mapper;

    protected override Task InnerHandleAsync(AutocompleteContext context)
    {
        context.ApiResponse = _mapper.ToApiResponse(context.RetailCompleteQueryResponse!);
        return Task.CompletedTask;
    }
}
