using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.Services.Mappers;

namespace VertexSearchApi.Tests.Mappers;

public class AutocompleteRequestMapperTests
{
    private static readonly GcpOptions DefaultGcp = new()
    {
        ProjectId = "test-project",
        Retail    = new GcpOptions.RetailOptions { Location = "global", Catalogs = "default_catalog" }
    };

    private AutocompleteRequestMapper CreateMapper(int defaultMax = 10, string dataset = "cloud-retail") =>
        new(Options.Create(DefaultGcp),
            Options.Create(new AutocompleteOptions
            {
                DefaultMaxSuggestions = defaultMax,
                MaxAllowedSuggestions = 20,
                Dataset               = dataset
            }));

    [Fact]
    public void Lowercases_Query()
    {
        var result = CreateMapper().ToRetailCompleteQueryRequest(
            new AutocompleteRequest { Query = "SHOES", VisitorId = "u1" });

        Assert.Equal("shoes", result.Query);
    }

    [Fact]
    public void Maps_VisitorId()
    {
        var result = CreateMapper().ToRetailCompleteQueryRequest(
            new AutocompleteRequest { Query = "sho", VisitorId = "visitor-42" });

        Assert.Equal("visitor-42", result.VisitorId);
    }

    [Fact]
    public void Uses_Default_MaxSuggestions_When_Not_Specified()
    {
        var result = CreateMapper(defaultMax: 10).ToRetailCompleteQueryRequest(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1" });

        Assert.Equal(10, result.MaxSuggestions);
    }

    [Fact]
    public void Uses_Provided_MaxSuggestions()
    {
        var result = CreateMapper().ToRetailCompleteQueryRequest(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1", MaxSuggestions = 5 });

        Assert.Equal(5, result.MaxSuggestions);
    }

    [Fact]
    public void Sets_Dataset()
    {
        var result = CreateMapper(dataset: "cloud-retail").ToRetailCompleteQueryRequest(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1" });

        Assert.Equal("cloud-retail", result.Dataset);
    }

    [Fact]
    public void Builds_Correct_Catalog_Path()
    {
        var result = CreateMapper().ToRetailCompleteQueryRequest(
            new AutocompleteRequest { Query = "sho", VisitorId = "u1" });

        Assert.Equal(
            "projects/test-project/locations/global/catalogs/default_catalog",
            result.Catalog);
    }
}
