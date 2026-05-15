# Vertex Search API — .NET 10

A **C# / ASP.NET Core 10** REST API wrapping **Google Cloud Vertex AI Retail Search** and **Vertex AI Retail Completion** (autocomplete).

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Project Structure](#project-structure)
3. [Prerequisites](#prerequisites)
4. [Configuration](#configuration)
5. [Google Cloud Authentication](#google-cloud-authentication)
6. [Running the Application](#running-the-application)
7. [Swagger UI](#swagger-ui)
8. [Health Check](#health-check)
9. [Rate Limiting](#rate-limiting)
10. [API Reference](#api-reference)
    - [POST /api/v1/search](#post-apiv1search)
    - [GET /api/v1/autocomplete](#get-apiv1autocomplete)
11. [Running Tests](#running-tests)
12. [Package Versions](#package-versions)

---

## Architecture Overview

The service uses a **chain-of-responsibility pipeline** — each request flows through a sequence of async steps. There are two independent pipelines:

```
POST /api/v1/search                        GET /api/v1/autocomplete
        │                                            │
        ▼                                            ▼
┌─────────────────────┐            ┌─────────────────────────────────┐
│ ValidateRequestStep │            │ ValidateAutocompleteRequestStep  │
└────────┬────────────┘            └────────────────┬────────────────┘
         │                                          │
         ▼                                          ▼
┌──────────────────────────┐       ┌─────────────────────────────────┐
│ ConvertRequestStep       │       │ ConvertAutocompleteRequestStep   │
│ (storeId → PlaceId,      │       │ (query lowercased)               │
│  sort/facet alias map)   │       └────────────────┬────────────────┘
└────────┬─────────────────┘                        │
         │                                          ▼
         ▼                             ┌─────────────────────────────────┐
┌──────────────────────────┐           │ AutocompleteStep                │
│ SearchStep               │           │ (CompletionServiceClient / gRPC) │
│ (SearchServiceClient     │           └────────────────┬────────────────┘
│  / gRPC, async)          │                            │
└────────┬─────────────────┘                            ▼
         │                             ┌─────────────────────────────────┐
         ▼                             │ ConvertAutocompleteResponseStep  │
┌──────────────────────────┐           └─────────────────────────────────┘
│ ConvertResponseStep      │
└──────────────────────────┘
```

Key design points:
- The entire pipeline is **fully async** — GCP gRPC calls use `SearchAsync` / `CompleteQueryAsync`, freeing thread pool threads while waiting.
- `SearchContext` / `AutocompleteContext` objects flow through each chain, accumulating state across steps.
- Steps are registered **Transient** in DI; mappers and GCP clients are **Singletons**.
- All GCP calls use `Google.Cloud.Retail.V2 v2.16.0` via gRPC.
- All DTOs are immutable **C# records**.

---

## Project Structure

```
vertex-search/
├── VertexSearchApi.csproj
├── Program.cs                           # DI wiring, middleware pipeline
├── appsettings.json                     # Gcp + Search + Autocomplete + RateLimit
├── appsettings.Development.json
│
├── Config/
│   ├── GcpOptions.cs                    # ProjectId, Location, Catalogs, Branch, Placement
│   ├── GcpPaths.cs                      # Branch(), Placement(), Catalog() path builders
│   ├── SearchOptions.cs                 # Paging defaults, sort aliases, facet key aliases
│   ├── AutocompleteOptions.cs           # MaxSuggestions, Dataset
│   └── RateLimitOptions.cs              # Per-endpoint rate limit windows
│
├── Controllers/
│   ├── SearchController.cs              # POST /api/v1/search
│   └── AutocompleteController.cs        # GET  /api/v1/autocomplete
│
├── DTOs/
│   ├── Request/
│   │   ├── KeywordSearchRequest.cs      # record with init properties
│   │   └── AutocompleteRequest.cs       # record with init properties
│   └── Response/
│       ├── KeywordSearchResponse.cs     # record with init properties
│       ├── ProductResult.cs             # record with init properties
│       ├── VariantResult.cs             # positional record
│       ├── FacetResult.cs               # positional record
│       ├── FacetValueResult.cs          # positional record
│       ├── SearchStats.cs               # positional record
│       ├── AutocompleteResponse.cs      # record
│       └── AutocompleteSuggestion.cs    # record
│
├── Exceptions/
│   ├── InvalidSearchRequestException.cs
│   ├── SearchServiceException.cs
│   └── AutocompleteServiceException.cs
│
├── Middleware/
│   └── GlobalExceptionHandler.cs        # Maps exceptions → JSON error + HTTP status
│
├── Services/
│   ├── Context/
│   │   ├── SearchContext.cs
│   │   └── AutocompleteContext.cs
│   ├── Mappers/
│   │   ├── SearchRequestMapper.cs       # KeywordSearchRequest → SearchRequest
│   │   ├── SearchResponseMapper.cs      # SearchResponse → KeywordSearchResponse
│   │   ├── AutocompleteRequestMapper.cs # AutocompleteRequest → CompleteQueryRequest
│   │   └── AutocompleteResponseMapper.cs
│   └── Pipeline/
│       ├── Step.cs                      # Abstract async base with latency logging
│       ├── StepService.cs               # Executes the first step and returns context
│       ├── Search/
│       │   ├── ValidateRequestStep.cs
│       │   ├── ConvertRequestStep.cs
│       │   ├── SearchStep.cs
│       │   └── ConvertResponseStep.cs
│       └── Autocomplete/
│           ├── ValidateAutocompleteRequestStep.cs
│           ├── ConvertAutocompleteRequestStep.cs
│           ├── AutocompleteStep.cs
│           └── ConvertAutocompleteResponseStep.cs
│
└── VertexSearchApi.Tests/
    ├── GlobalUsings.cs
    ├── Mappers/
    │   ├── SearchRequestMapperTests.cs
    │   ├── SearchResponseMapperTests.cs
    │   ├── AutocompleteRequestMapperTests.cs
    │   └── AutocompleteResponseMapperTests.cs
    └── Validators/
        ├── ValidateRequestStepTests.cs
        └── ValidateAutocompleteRequestStepTests.cs
```

---

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** |
| [Google Cloud SDK (`gcloud`)](https://cloud.google.com/sdk) | any recent |
| GCP project with **Retail API** enabled | — |

```bash
dotnet --version   # should print 10.x.x
```

---

## Configuration

All settings live in `appsettings.json`. Override at runtime using `__` as a section separator:

```bash
export Gcp__ProjectId=my-gcp-project
```

### GCP Settings (`Gcp`)

| Key | Default | Description |
|---|---|---|
| `Gcp:ProjectId` | *(empty)* | **Required.** Your GCP project ID |
| `Gcp:Retail:Location` | `global` | Retail API region |
| `Gcp:Retail:Catalogs` | `default_catalog` | Catalog name in Vertex AI |
| `Gcp:Retail:Branch` | `default_branch` | Catalog branch |
| `Gcp:Retail:Placement` | `default_search` | Serving config name for search |

### Search Defaults (`Search`)

| Key | Default | Description |
|---|---|---|
| `Search:DefaultPageSize` | `20` | Page size when not specified in request |
| `Search:MaxPageSize` | `100` | Maximum allowed page size (validated) |
| `Search:DefaultOffset` | `0` | Pagination offset when not specified |
| `Search:SortMap` | see below | Client sort alias → Vertex AI `orderBy` expression |
| `Search:FacetKeys` | see below | Client facet alias → Vertex AI attribute name |

**Sort aliases:**

| Client alias | Vertex `orderBy` |
|---|---|
| `relevance` | *(empty — Vertex default)* |
| `price_low_to_high` | `price` |
| `price_high_to_low` | `price desc` |
| `newest` | `attributes.createTime desc` |

**Facet key aliases:**

| Client alias | Vertex attribute |
|---|---|
| `color` | `colorFamilies` |
| `size` | `sizes` |
| `brand` | `brands` |
| `category` | `categories` |
| `price` | `price` |

### Autocomplete Settings (`Autocomplete`)

| Key | Default | Description |
|---|---|---|
| `Autocomplete:DefaultMaxSuggestions` | `10` | Used when `maxSuggestions` is not provided |
| `Autocomplete:MaxAllowedSuggestions` | `20` | Validation ceiling — requests above this return 400 |
| `Autocomplete:Dataset` | `cloud-retail` | Vertex AI Retail dataset to query |

### Rate Limit Settings (`RateLimit`)

| Key | Default | Description |
|---|---|---|
| `RateLimit:Search:PermitLimit` | `60` | Max requests per window |
| `RateLimit:Search:WindowSeconds` | `60` | Window duration in seconds |
| `RateLimit:Autocomplete:PermitLimit` | `200` | Max requests per window |
| `RateLimit:Autocomplete:WindowSeconds` | `60` | Window duration in seconds |

Limits are applied **per client IP**. A `429 Too Many Requests` response includes a `Retry-After` header.

---

## Google Cloud Authentication

Both `SearchServiceClient` and `CompletionServiceClient` use **Application Default Credentials (ADC)**.

### Option 1 — Developer workstation

```bash
gcloud auth application-default login
```

### Option 2 — Service account key file

```bash
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account-key.json
```

### Option 3 — Workload Identity (GKE / Cloud Run)

No configuration needed when the GCP service account is attached to the workload.

> **Required IAM roles:** `roles/retail.viewer` (minimum) or `roles/retail.admin`

---

## Running the Application

```bash
export Gcp__ProjectId=YOUR_GCP_PROJECT_ID
dotnet restore
dotnet build VertexSearchApi.csproj
dotnet run --project VertexSearchApi.csproj
```

Expected startup output:

```
info: Warming up SearchServiceClient...
info: SearchServiceClient warm-up completed in 340ms
info: Now listening on: http://localhost:5000
```

The warm-up call pre-establishes the gRPC connection to Google. It may log a warning on empty catalogs — this is expected and non-fatal.

---

## Swagger UI

```
http://localhost:5000/swagger
```

Both endpoints are documented and testable directly in the browser.

---

## Health Check

```
GET /health
```

Returns `200 OK` with:

```json
{
  "status": "Healthy",
  "uptime": "0.00:12:34"
}
```

Returns `503 Service Unavailable` if any registered health check fails. Suitable for Kubernetes liveness and readiness probes.

---

## Rate Limiting

All endpoints are rate-limited per client IP using a fixed-window limiter:

| Endpoint | Limit |
|---|---|
| `POST /api/v1/search` | 60 requests / 60 s |
| `GET /api/v1/autocomplete` | 200 requests / 60 s |

When the limit is exceeded:

```
HTTP 429 Too Many Requests
Retry-After: 45
{ "error": "Too many requests. Please slow down." }
```

Limits and window sizes are configurable via `appsettings.json` under `RateLimit`.

---

## API Reference

### `POST /api/v1/search`

Keyword search against the Vertex AI Retail catalog. Requires GCP credentials.

**Request body (`application/json`):**

```json
{
  "query": "red running shoes",
  "visitorId": "user-abc-123",
  "pageSize": 20,
  "offset": 0,
  "orderBy": "price_low_to_high",
  "filter": "availability: IN_STOCK",
  "facetKeys": ["color", "brand", "size"],
  "queryExpansionCondition": "AUTO",
  "storeId": "store-042"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `query` | string | **Yes** | Search query text |
| `visitorId` | string | **Yes** | Visitor/session ID for analytics |
| `pageSize` | integer | No | Results per page (default 20, max 100) |
| `offset` | integer | No | Pagination offset (default 0) |
| `orderBy` | string | No | Sort alias: `relevance`, `price_low_to_high`, `price_high_to_low`, `newest` |
| `filter` | string | No | Vertex AI Retail filter expression |
| `facetKeys` | string[] | No | Facet aliases: `color`, `size`, `brand`, `category`, `price` |
| `queryExpansionCondition` | string | No | `AUTO` or `DISABLED` (default `AUTO`) |
| `storeId` | string | No | Scope results to a specific store (maps to `SearchRequest.PlaceId`) |

**Response (`200 OK`):**

```json
{
  "products": [
    {
      "id": "product-001",
      "title": "Red Running Shoes",
      "categories": ["Footwear > Running"],
      "uri": "https://example.com/products/001",
      "attributes": { "color": "Red", "brand": "Nike" },
      "variants": [{ "id": "variant-001-sz10" }]
    }
  ],
  "facets": [
    {
      "key": "colorFamilies",
      "values": [{ "value": "Red", "count": 42 }]
    }
  ],
  "stats": { "returned": 20, "totalResults": 157, "offset": 0 },
  "correctedQuery": "red running shoes",
  "attributionToken": "AbCdEfGh...",
  "appliedControls": ["boost-new-arrivals"]
}
```

Fields omitted from the response when empty/null (configured via `WhenWritingNull`).

**Error responses:**

| Status | Condition |
|---|---|
| `400` | Missing `query` / `visitorId`, invalid `pageSize` / `offset`, malformed JSON |
| `401` | Missing or invalid GCP credentials |
| `403` | Service account lacks required IAM roles |
| `404` | Catalog or branch not found |
| `429` | Rate limit exceeded |
| `503` | Vertex AI service unavailable |

---

### `GET /api/v1/autocomplete`

Returns typeahead suggestions from the Vertex AI Retail Completion API. Requires GCP credentials.

**Query parameters:**

| Parameter | Required | Default | Description |
|---|---|---|---|
| `query` | **Yes** | — | Partial search term. Automatically lowercased before sending to GCP. |
| `visitorId` | **Yes** | — | Visitor/session identifier |
| `maxSuggestions` | No | `10` | Number of suggestions to return. Must be between 1 and 20. |

**Example request:**

```
GET /api/v1/autocomplete?query=run&visitorId=user-123&maxSuggestions=5
```

**Response (`200 OK`):**

```json
{
  "suggestions": [
    { "suggestion": "running shoes" },
    { "suggestion": "runners" },
    { "suggestion": "running tights" },
    { "suggestion": "running socks" },
    { "suggestion": "running jacket" }
  ],
  "attributionToken": "AHRlcnJpZmljLXRva2Vu..."
}
```

> Pass `attributionToken` back to Vertex AI with any subsequent search triggered by selecting a suggestion — it improves personalisation and analytics.

**Error responses:**

| Status | Condition |
|---|---|
| `400` | Missing `query` / `visitorId`, `maxSuggestions` outside 1–20 |
| `401` | Missing or invalid GCP credentials |
| `403` | Service account lacks required IAM roles |
| `429` | Rate limit exceeded |
| `503` | Vertex AI Completion service unavailable |

---

## Running Tests

```bash
dotnet test VertexSearchApi.Tests/VertexSearchApi.Tests.csproj
```

**68 unit tests** covering:

| Suite | Tests |
|---|---|
| `SearchRequestMapperTests` | Sort alias resolution, facet key mapping, GCP path building, query expansion, filter, storeId, pagination defaults |
| `SearchResponseMapperTests` | Product ID fallback logic, attribute mapping, facet mapping, stats, null handling |
| `AutocompleteRequestMapperTests` | Query lowercasing, MaxSuggestions defaults, catalog path building, dataset |
| `AutocompleteResponseMapperTests` | Suggestion mapping, attribution token, empty response |
| `ValidateRequestStepTests` | Required fields, pageSize/offset bounds |
| `ValidateAutocompleteRequestStepTests` | Required fields, maxSuggestions bounds |

No GCP credentials required — all tests use in-memory data and `NullLogger`.

---

## Package Versions

| Package | Version |
|---|---|
| `Google.Cloud.Retail.V2` | `2.16.0` |
| `Swashbuckle.AspNetCore` | `7.2.0` |
| `Microsoft.NET.Test.Sdk` | `17.11.1` |
| `xunit` | `2.9.2` |
| `coverlet.collector` | `6.0.2` |
