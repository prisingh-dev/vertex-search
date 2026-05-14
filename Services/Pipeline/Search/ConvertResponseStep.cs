using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Search;

public class ConvertResponseStep : Step<SearchContext>
{
    private readonly SearchResponseMapper _mapper;

    public ConvertResponseStep(ILogger<ConvertResponseStep> logger, SearchResponseMapper mapper)
        : base(logger) => _mapper = mapper;

    protected override void InnerHandle(SearchContext context)
        => context.ApiResponse = _mapper.ToApiResponse(context);
}
