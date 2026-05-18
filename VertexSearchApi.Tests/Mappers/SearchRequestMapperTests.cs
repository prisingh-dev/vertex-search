using Microsoft.Extensions.Options;
using VertexSearchApi.Config;
using VertexSearchApi.DTOs.Request;
using VertexSearchApi.Services.Mappers;

namespace VertexSearchApi.Tests.Mappers;

public class SearchRequestMapperTests
{
    private static readonly GcpOptions DefaultGcp = new()
    {
        ProjectId = "test-project",
        Retail = new GcpOptions.RetailOptions
        {
            Location  = "global",
            Catalogs  = "default_catalog",
            Branch    = "default_branch",
            Placement = "default_search"
        }
    };

    private static readonly SearchOptions DefaultSearch = new()
    {
        DefaultPageSize = 20,
        MaxPageSize     = 100,
        SortMap = new Dictionary<string, string>
        {
            ["relevance"]         = "",
            ["price_low_to_high"] = "price",
            ["price_high_to_low"] = "price desc"
        },
        FacetKeys = new Dictionary<string, string>
        {
            ["color"] = "colorFamilies",
            ["brand"] = "brands"
        }
    };

    private SearchRequestMapper CreateMapper(GcpOptions? gcp = null, SearchOptions? search = null) =>
        new(Options.Create(gcp ?? DefaultGcp), Options.Create(search ?? DefaultSearch));

    [Fact]
    public void Maps_Query_And_VisitorId()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "user-abc" });

        Assert.Equal("shoes", result.Query);
        Assert.Equal("user-abc", result.VisitorId);
    }

    [Fact]
    public void Uses_Default_PageSize_When_Not_Specified()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1" });

        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public void Sets_PageToken_When_Provided()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", PageToken = "next-page" });

        Assert.Equal("next-page", result.PageToken);
    }

    [Fact]
    public void Uses_Provided_PageSize()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", PageSize = 10 });

        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public void Resolves_Sort_Alias()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", OrderBy = "price_low_to_high" });

        Assert.Equal("price", result.OrderBy);
    }

    [Fact]
    public void Omits_OrderBy_For_Relevance_Sort()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", OrderBy = "relevance" });

        Assert.Equal(string.Empty, result.OrderBy);
    }

    [Fact]
    public void Passes_Unknown_Sort_Through_Unmodified()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", OrderBy = "custom_sort" });

        Assert.Equal("custom_sort", result.OrderBy);
    }

    [Fact]
    public void Resolves_Facet_Key_Alias()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", FacetKeys = ["color"] });

        Assert.Single(result.FacetSpecs);
        Assert.Equal("colorFamilies", result.FacetSpecs[0].FacetKey.Key);
    }

    [Fact]
    public void Passes_Unknown_Facet_Key_Through_Unmodified()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", FacetKeys = ["custom_facet"] });

        Assert.Single(result.FacetSpecs);
        Assert.Equal("custom_facet", result.FacetSpecs[0].FacetKey.Key);
    }

    [Fact]
    public void Maps_Multiple_Facet_Keys()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", FacetKeys = ["color", "brand"] });

        Assert.Equal(2, result.FacetSpecs.Count);
        Assert.Equal("colorFamilies", result.FacetSpecs[0].FacetKey.Key);
        Assert.Equal("brands", result.FacetSpecs[1].FacetKey.Key);
    }

    [Fact]
    public void Sets_Filter_When_Provided()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", Filter = "price < 100" });

        Assert.Equal("price < 100", result.Filter);
    }

    [Fact]
    public void Omits_Filter_When_Null()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1" });

        Assert.Equal(string.Empty, result.Filter);
    }

    [Fact]
    public void Sets_StoreId_As_PlaceId_When_Provided()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", StoreId = "store-42" });

        Assert.Equal("store-42", result.PlaceId);
    }

    [Fact]
    public void Builds_Correct_Branch_Path()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1" });

        Assert.Equal(
            "projects/test-project/locations/global/catalogs/default_catalog/branches/default_branch",
            result.Branch);
    }

    [Fact]
    public void Builds_Correct_Placement_Path()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1" });

        Assert.Equal(
            "projects/test-project/locations/global/catalogs/default_catalog/servingConfigs/default_search",
            result.Placement);
    }

    [Fact]
    public void Parses_QueryExpansionCondition_Case_Insensitive()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", QueryExpansionCondition = "DISABLED" });

        Assert.Equal(
            Google.Cloud.Retail.V2.SearchRequest.Types.QueryExpansionSpec.Types.Condition.Disabled,
            result.QueryExpansionSpec.Condition);
    }

    [Fact]
    public void Falls_Back_To_Auto_QueryExpansion_For_Unknown_Value()
    {
        var result = CreateMapper().ToRetailSearchRequest(
            new KeywordSearchRequest { Query = "shoes", VisitorId = "u1", QueryExpansionCondition = "bogus" });

        Assert.Equal(
            Google.Cloud.Retail.V2.SearchRequest.Types.QueryExpansionSpec.Types.Condition.Auto,
            result.QueryExpansionSpec.Condition);
    }
}
