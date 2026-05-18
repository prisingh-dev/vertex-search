using Google.Cloud.Retail.V2;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;

namespace VertexSearchApi.Tests.Mappers;

public class SearchResponseMapperTests
{
    private readonly SearchResponseMapper _mapper = new();

    private static SearchContext BuildContext(SearchResponse response) => new()
    {
        RetailSearchRequest  = new SearchRequest { PageSize = 20 },
        RetailSearchResponse = response
    };

    private static SearchResponse SingleProductResponse(Action<Product>? configure = null)
    {
        var product = new Product { Id = "p1", PrimaryProductId = "p1", Title = "Red Shoes", Uri = "https://example.com/p1" };
        configure?.Invoke(product);
        var response = new SearchResponse { TotalSize = 1 };
        response.Results.Add(new SearchResponse.Types.SearchResult { Id = "p1", Product = product });
        return response;
    }

    [Fact]
    public void Maps_Product_Id_Title_Uri()
    {
        var result = _mapper.ToApiResponse(BuildContext(SingleProductResponse()));

        Assert.Single(result.Products!);
        Assert.Equal("p1", result.Products![0].Id);
        Assert.Equal("Red Shoes", result.Products[0].Title);
        Assert.Equal("https://example.com/p1", result.Products[0].Uri);
    }

    [Fact]
    public void Prefers_PrimaryProductId_Over_Id()
    {
        var response = new SearchResponse { TotalSize = 1 };
        response.Results.Add(new SearchResponse.Types.SearchResult
        {
            Id      = "result-id",
            Product = new Product { Id = "product-id", PrimaryProductId = "primary-id" }
        });

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal("primary-id", result.Products![0].Id);
    }

    [Fact]
    public void Falls_Back_To_Product_Id_When_PrimaryProductId_Empty()
    {
        var response = new SearchResponse { TotalSize = 1 };
        response.Results.Add(new SearchResponse.Types.SearchResult
        {
            Id      = "result-id",
            Product = new Product { Id = "product-id", PrimaryProductId = "" }
        });

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal("product-id", result.Products![0].Id);
    }

    [Fact]
    public void Maps_Product_Categories()
    {
        var response = SingleProductResponse(p => p.Categories.AddRange(["Footwear", "Footwear > Running"]));

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal(2, result.Products![0].Categories!.Count);
        Assert.Contains("Footwear > Running", result.Products[0].Categories!);
    }

    [Fact]
    public void Maps_Product_Text_Attributes()
    {
        var response = SingleProductResponse(p =>
        {
            var attr = new CustomAttribute();
            attr.Text.Add("Red");
            p.Attributes["color"] = attr;
        });

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal("Red", result.Products![0].Attributes!["color"]);
    }

    [Fact]
    public void Maps_Product_Numeric_Attributes()
    {
        var response = SingleProductResponse(p =>
        {
            var attr = new CustomAttribute();
            attr.Numbers.Add(99.99);
            p.Attributes["price"] = attr;
        });

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal("99.99", result.Products![0].Attributes!["price"]);
    }

    [Fact]
    public void Maps_Product_Variants()
    {
        var response = SingleProductResponse(p =>
            p.Variants.AddRange([new Product { Id = "p1-sz9" }, new Product { Id = "p1-sz10" }]));

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal(2, result.Products![0].Variants!.Count);
        Assert.Equal("p1-sz9",  result.Products![0].Variants![0].Id);
        Assert.Equal("p1-sz10", result.Products![0].Variants![1].Id);
    }

    [Fact]
    public void Returns_Null_Products_When_Response_Empty()
    {
        var result = _mapper.ToApiResponse(BuildContext(new SearchResponse { TotalSize = 0 }));

        Assert.Null(result.Products);
    }

    [Fact]
    public void Maps_Facets_With_Values()
    {
        var response = SingleProductResponse();
        var facet = new SearchResponse.Types.Facet { Key = "colorFamilies" };
        facet.Values.AddRange([
            new SearchResponse.Types.Facet.Types.FacetValue { Value = "Red",  Count = 10 },
            new SearchResponse.Types.Facet.Types.FacetValue { Value = "Blue", Count = 5  }
        ]);
        response.Facets.Add(facet);

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Single(result.Facets!);
        Assert.Equal("colorFamilies", result.Facets![0].Key);
        Assert.Equal(2, result.Facets[0].Values!.Count);
        Assert.Equal("Red", result.Facets![0].Values![0].Value);
        Assert.Equal(10,    result.Facets![0].Values![0].Count);
    }

    [Fact]
    public void Returns_Null_Facets_When_None_In_Response()
    {
        var result = _mapper.ToApiResponse(BuildContext(new SearchResponse { TotalSize = 0 }));

        Assert.Null(result.Facets);
    }

    [Fact]
    public void Maps_Stats_Returned_And_TotalResults()
    {
        var response = SingleProductResponse();
        response.TotalSize = 42;

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal(1,  result.Stats!.Returned);
        Assert.Equal(42, result.Stats.TotalResults);
    }

    [Fact]
    public void Sets_CorrectedQuery_When_Present()
    {
        var response = new SearchResponse { TotalSize = 0, CorrectedQuery = "shoes" };

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal("shoes", result.CorrectedQuery);
    }

    [Fact]
    public void Returns_Null_CorrectedQuery_When_Empty_String()
    {
        var response = new SearchResponse { TotalSize = 0, CorrectedQuery = "" };

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Null(result.CorrectedQuery);
    }

    [Fact]
    public void Maps_AttributionToken()
    {
        var response = new SearchResponse { TotalSize = 0, AttributionToken = "tok-abc" };

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal("tok-abc", result.AttributionToken);
    }

    [Fact]
    public void Maps_AppliedControls()
    {
        var response = new SearchResponse { TotalSize = 0 };
        response.AppliedControls.AddRange(["boost-sale", "pin-new"]);

        var result = _mapper.ToApiResponse(BuildContext(response));

        Assert.Equal(2, result.AppliedControls!.Count);
        Assert.Contains("boost-sale", result.AppliedControls);
    }

    [Fact]
    public void Returns_Null_AppliedControls_When_None()
    {
        var result = _mapper.ToApiResponse(BuildContext(new SearchResponse { TotalSize = 0 }));

        Assert.Null(result.AppliedControls);
    }
}
