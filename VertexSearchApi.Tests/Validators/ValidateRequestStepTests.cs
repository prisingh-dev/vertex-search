using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.Exceptions;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline.Search;

namespace VertexSearchApi.Tests.Validators;

public class ValidateRequestStepTests
{
    private static ValidateRequestStep CreateStep(int maxPageSize = 100) =>
        new(NullLogger<ValidateRequestStep>.Instance,
            Options.Create(new SearchOptions { MaxPageSize = maxPageSize }));

    private static SearchContext ContextWith(KeywordSearchRequest request) =>
        new() { ApiRequest = request };

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Valid_Request_Does_Not_Throw()
    {
        await CreateStep().HandleAsync(ContextWith(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1" }));
    }

    [Fact]
    public async Task All_Optional_Fields_Null_Does_Not_Throw()
    {
        await CreateStep().HandleAsync(ContextWith(new KeywordSearchRequest
        {
            Query    = "shoes",
            VisitorId = "u1",
            PageSize  = null,
            Offset    = null,
            OrderBy   = null,
            Filter    = null,
            FacetKeys = null
        }));
    }

    [Fact]
    public async Task PageSize_At_Max_Does_Not_Throw()
    {
        await CreateStep(maxPageSize: 50).HandleAsync(ContextWith(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", PageSize = 50 }));
    }

    [Fact]
    public async Task Zero_Offset_Does_Not_Throw()
    {
        await CreateStep().HandleAsync(ContextWith(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", Offset = 0 }));
    }

    // ── Null / missing fields ─────────────────────────────────────────────────

    [Fact]
    public async Task Null_ApiRequest_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(new SearchContext { ApiRequest = null }));

        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Empty_Query_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "", VisitorId = "u1" })));
    }

    [Fact]
    public async Task Whitespace_Query_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "   ", VisitorId = "u1" })));
    }

    [Fact]
    public async Task Null_Query_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = null, VisitorId = "u1" })));
    }

    [Fact]
    public async Task Empty_VisitorId_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "shoes", VisitorId = "" })));
    }

    [Fact]
    public async Task Null_VisitorId_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "shoes", VisitorId = null })));
    }

    // ── PageSize bounds ───────────────────────────────────────────────────────

    [Fact]
    public async Task PageSize_Zero_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", PageSize = 0 })));
    }

    [Fact]
    public async Task PageSize_Negative_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", PageSize = -1 })));
    }

    [Fact]
    public async Task PageSize_Exceeds_Max_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep(maxPageSize: 50).HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", PageSize = 51 })));
    }

    // ── Offset bounds ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Negative_Offset_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", Offset = -1 })));
    }
}
