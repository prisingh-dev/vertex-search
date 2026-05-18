using System.Diagnostics;
using System.Threading.RateLimiting;
using Google.Cloud.Retail.V2;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VertexSearchApi.Config;
using VertexSearchApi.Middleware;
using VertexSearchApi.Services.Context;
using VertexSearchApi.Services.Mappers;
using VertexSearchApi.Services.Pipeline;
using VertexSearchApi.Services.Pipeline.Autocomplete;
using VertexSearchApi.Services.Pipeline.Search;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GcpOptions>(
    builder.Configuration.GetSection(GcpOptions.SectionName));
builder.Services.Configure<SearchOptions>(
    builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.Configure<AutocompleteOptions>(
    builder.Configuration.GetSection(AutocompleteOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vertex Search API",
        Version = "v1",
        Description = "REST wrapper for Google Cloud Vertex AI Retail Search"
    }));

builder.Services.AddHealthChecks();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(limiter =>
{
    var rl = builder.Configuration
        .GetSection(RateLimitOptions.SectionName)
        .Get<RateLimitOptions>() ?? new RateLimitOptions();

    limiter.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down." }, ct);
    };

    static string IpKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    limiter.AddPolicy(RateLimitOptions.Policies.Search, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: IpKey(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = rl.Search.PermitLimit,
                Window            = TimeSpan.FromSeconds(rl.Search.WindowSeconds),
                QueueLimit        = rl.Search.QueueLimit,
                AutoReplenishment = true
            }));

    limiter.AddPolicy(RateLimitOptions.Policies.Autocomplete, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: IpKey(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit       = rl.Autocomplete.PermitLimit,
                Window            = TimeSpan.FromSeconds(rl.Autocomplete.WindowSeconds),
                QueueLimit        = rl.Autocomplete.QueueLimit,
                AutoReplenishment = true
            }));
});

builder.Services.AddSingleton<SearchServiceClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var gcpOptions = sp.GetRequiredService<IOptions<GcpOptions>>().Value;

    var clientBuilder = new SearchServiceClientBuilder
    {
        QuotaProject = gcpOptions.ProjectId
    };
    var client = clientBuilder.Build();

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

builder.Services.AddSingleton<CompletionServiceClient>(sp =>
{
    var gcpOptions = sp.GetRequiredService<IOptions<GcpOptions>>().Value;
    return new CompletionServiceClientBuilder
    {
        QuotaProject = gcpOptions.ProjectId
    }.Build();
});

builder.Services.AddSingleton<SearchRequestMapper>();
builder.Services.AddSingleton<SearchResponseMapper>();
builder.Services.AddSingleton<AutocompleteRequestMapper>();
builder.Services.AddSingleton<AutocompleteResponseMapper>();

builder.Services.AddTransient<ValidateRequestStep>();
builder.Services.AddTransient<ConvertRequestStep>();
builder.Services.AddTransient<SearchStep>();
builder.Services.AddTransient<ConvertResponseStep>();

builder.Services.AddTransient<ValidateAutocompleteRequestStep>();
builder.Services.AddTransient<ConvertAutocompleteRequestStep>();
builder.Services.AddTransient<AutocompleteStep>();
builder.Services.AddTransient<ConvertAutocompleteResponseStep>();

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

builder.Services.AddSingleton<StepService<AutocompleteContext>>(sp =>
{
    var validate     = sp.GetRequiredService<ValidateAutocompleteRequestStep>();
    var convertReq   = sp.GetRequiredService<ConvertAutocompleteRequestStep>();
    var autocomplete = sp.GetRequiredService<AutocompleteStep>();
    var convertResp  = sp.GetRequiredService<ConvertAutocompleteResponseStep>();

    validate.SetNextStep(convertReq);
    convertReq.SetNextStep(autocomplete);
    autocomplete.SetNextStep(convertResp);

    return new StepService<AutocompleteContext>(validate);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(c =>
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vertex Search API v1"));

app.MapControllers();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status  = report.Status.ToString(),
            uptime  = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm\:ss")
        });
    }
});

app.Run();
