using System.Diagnostics;
using Google.Cloud.Retail.V2;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VertexSearchApi.Config;
using VertexSearchApi.Middleware;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;
using VertexSearchApi.Services.Pipeline;
using VertexSearchApi.Services.Pipeline.Search;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<GcpOptions>(
    builder.Configuration.GetSection(GcpOptions.SectionName));
builder.Services.Configure<SearchOptions>(
    builder.Configuration.GetSection(SearchOptions.SectionName));

// ─── HTTP / JSON ──────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

// ─── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vertex Search API",
        Version = "v1",
        Description = "REST wrapper for Google Cloud Vertex AI Retail Search"
    }));

// ─── Exception handling ───────────────────────────────────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ─── Google Cloud Retail SearchServiceClient ──────────────────────────────────
builder.Services.AddSingleton<SearchServiceClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var gcpOptions = sp.GetRequiredService<IOptions<GcpOptions>>().Value;

    var clientBuilder = new SearchServiceClientBuilder
    {
        QuotaProject = gcpOptions.ProjectId
    };
    var client = clientBuilder.Build();

    // Warm-up call to initialise gRPC connections (may fail on empty catalogs)
    try
    {
        logger.LogInformation("Warming up SearchServiceClient...");
        var sw = Stopwatch.StartNew();
        client.Search(new SearchRequest
        {
            Branch    = GcpPaths.Branch(gcpOptions),
            Placement = GcpPaths.Placement(gcpOptions),
            VisitorId = "warmup",
            Query     = "a",
            PageSize  = 1
        }).AsRawResponses().FirstOrDefault();
        logger.LogInformation(
            "SearchServiceClient warm-up completed in {Ms}ms", sw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        logger.LogWarning(
            "SearchServiceClient warm-up failed (expected for empty catalogs): {Message}",
            ex.Message);
    }

    return client;
});

// ─── Mappers ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SearchRequestMapper>();
builder.Services.AddSingleton<SearchResponseMapper>();

// ─── Pipeline steps ───────────────────────────────────────────────────────────
// Transient so the live chain and the mock chain each capture their own instances.
// (A singleton step stores its NextStep as instance state — two chains can't share one.)
builder.Services.AddTransient<ValidateRequestStep>();
builder.Services.AddTransient<ConvertRequestStep>();
builder.Services.AddTransient<SearchStep>();
builder.Services.AddTransient<ConvertResponseStep>();
builder.Services.AddTransient<MockSearchStep>();

// Live chain:  Validate → ConvertRequest → Search → ConvertResponse
builder.Services.AddSingleton<StepService<SearchContext>>(sp =>
{
    var validate    = sp.GetRequiredService<ValidateRequestStep>();
    var convertReq  = sp.GetRequiredService<ConvertRequestStep>();
    var search      = sp.GetRequiredService<SearchStep>();
    var convertResp = sp.GetRequiredService<ConvertResponseStep>();

    validate.SetNextStep(convertReq);
    convertReq.SetNextStep(search);
    search.SetNextStep(convertResp);

    return new StepService<SearchContext>(validate);
});

// Mock chain:  Validate → ConvertRequest → MockSearch → ConvertResponse
// Identical to the live chain except the GCP call is replaced with fixture data.
builder.Services.AddKeyedSingleton<StepService<SearchContext>>("mock", (sp, _) =>
{
    var validate    = sp.GetRequiredService<ValidateRequestStep>();
    var convertReq  = sp.GetRequiredService<ConvertRequestStep>();
    var mockSearch  = sp.GetRequiredService<MockSearchStep>();
    var convertResp = sp.GetRequiredService<ConvertResponseStep>();

    validate.SetNextStep(convertReq);
    convertReq.SetNextStep(mockSearch);
    mockSearch.SetNextStep(convertResp);

    return new StepService<SearchContext>(validate);
});

// ─── Build & configure middleware pipeline ────────────────────────────────────
var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vertex Search API v1"));

app.MapControllers();

app.Run();
