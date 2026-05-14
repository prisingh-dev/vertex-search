using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.Exceptions;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline;

namespace VertexSearchApi.Services.Pipeline.Search;

public class ValidateRequestStep : Step<SearchContext>
{
    private readonly SearchOptions _opts;

    public ValidateRequestStep(ILogger<ValidateRequestStep> logger, IOptions<SearchOptions> opts)
        : base(logger) => _opts = opts.Value;

    protected override void InnerHandle(SearchContext context)
    {
        var req = context.ApiRequest
            ?? throw new InvalidSearchRequestException("Request body is required");

        if (string.IsNullOrWhiteSpace(req.Query))
            throw new InvalidSearchRequestException("query is required");

        if (string.IsNullOrWhiteSpace(req.VisitorId))
            throw new InvalidSearchRequestException("visitorId is required");

        if (req.PageSize.HasValue)
        {
            if (req.PageSize < 1)
                throw new InvalidSearchRequestException("pageSize must be >= 1");
            if (req.PageSize > _opts.MaxPageSize)
                throw new InvalidSearchRequestException($"pageSize must be <= {_opts.MaxPageSize}");
        }

        if (req.Offset.HasValue && req.Offset < 0)
            throw new InvalidSearchRequestException("offset must be >= 0");
    }
}
