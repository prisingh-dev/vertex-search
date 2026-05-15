using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Search;

public class ConvertRequestStep : Step<SearchContext>
{
    private readonly SearchRequestMapper _mapper;

    public ConvertRequestStep(ILogger<ConvertRequestStep> logger, SearchRequestMapper mapper)
        : base(logger) => _mapper = mapper;

    protected override Task InnerHandleAsync(SearchContext context)
    {
        context.RetailSearchRequest = _mapper.ToRetailSearchRequest(context.ApiRequest!);
        return Task.CompletedTask;
    }
}
