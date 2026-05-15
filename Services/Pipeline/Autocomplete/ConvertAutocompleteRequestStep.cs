using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Autocomplete;

public class ConvertAutocompleteRequestStep : Step<AutocompleteContext>
{
    private readonly AutocompleteRequestMapper _mapper;

    public ConvertAutocompleteRequestStep(
        ILogger<ConvertAutocompleteRequestStep> logger,
        AutocompleteRequestMapper mapper)
        : base(logger) => _mapper = mapper;

    protected override Task InnerHandleAsync(AutocompleteContext context)
    {
        context.RetailCompleteQueryRequest =
            _mapper.ToRetailCompleteQueryRequest(context.ApiRequest!);
        return Task.CompletedTask;
    }
}
