using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.Exceptions;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Pipeline.Autocomplete;

namespace VertexSearchApi.Tests.Validators;

public class ValidateAutocompleteRequestStepTests
{
    private static ValidateAutocompleteRequestStep CreateStep(int maxSuggestions = 20) =>
        new(NullLogger<ValidateAutocompleteRequestStep>.Instance,
            Options.Create(new AutocompleteOptions { MaxAllowedSuggestions = maxSuggestions }));

    private static AutocompleteContext ContextWith(AutocompleteRequest request) =>
        new() { ApiRequest = request };

    [Fact]
    public async Task Valid_Request_Does_Not_Throw()
    {
        await CreateStep().HandleAsync(ContextWith(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1" }));
    }

    [Fact]
    public async Task Null_MaxSuggestions_Does_Not_Throw()
    {
        await CreateStep().HandleAsync(ContextWith(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1", MaxSuggestions = null }));
    }

    [Fact]
    public async Task MaxSuggestions_At_Max_Does_Not_Throw()
    {
        await CreateStep(maxSuggestions: 10).HandleAsync(ContextWith(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1", MaxSuggestions = 10 }));
    }

    [Fact]
    public async Task Null_ApiRequest_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(new AutocompleteContext { ApiRequest = null }));
    }

    [Fact]
    public async Task Empty_Query_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "", VisitorId = "u1" })));
    }

    [Fact]
    public async Task Whitespace_Query_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "  ", VisitorId = "u1" })));
    }

    [Fact]
    public async Task Null_Query_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = null, VisitorId = "u1" })));
    }

    [Fact]
    public async Task Empty_VisitorId_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "sho", VisitorId = "" })));
    }

    [Fact]
    public async Task Null_VisitorId_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "sho", VisitorId = null })));
    }

    [Fact]
    public async Task MaxSuggestions_Zero_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "sho", VisitorId = "u1", MaxSuggestions = 0 })));
    }

    [Fact]
    public async Task MaxSuggestions_Negative_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep().HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "sho", VisitorId = "u1", MaxSuggestions = -1 })));
    }

    [Fact]
    public async Task MaxSuggestions_Exceeds_Max_Throws()
    {
        await Assert.ThrowsAsync<InvalidSearchRequestException>(
            () => CreateStep(maxSuggestions: 10).HandleAsync(ContextWith(
                new AutocompleteRequest { Query = "sho", VisitorId = "u1", MaxSuggestions = 11 })));
    }
}
