# Vertex Search API — .NET 10

A **C# / ASP.NET Core 10** port of the Java Spring Boot `vertex-search-api` service.  
It wraps **Google Cloud Vertex AI Retail Search** behind a clean REST endpoint and exposes interactive API docs via Swagger UI.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Project Structure](#project-structure)
3. [Prerequisites](#prerequisites)
4. [Quick Start — Mock Mode (no GCP credentials needed)](#quick-start--mock-mode-no-gcp-credentials-needed)
5. [Configuration](#configuration)
6. [Google Cloud Authentication](#google-cloud-authentication)
7. [Running the Application (live GCP)](#running-the-application-live-gcp)
8. [Swagger UI](#swagger-ui)
9. [API Reference](#api-reference)
   - [POST /api/v1/search](#post-apiv1search)
   - [POST /api/v1/search/mock](#post-apiv1searchmock)
10. [Package Versions & Compatibility](#package-versions--compatibility)

---

## Architecture Overview

The service uses a **chain-of-responsibility pipeline** identical to the original Java implementation:

```
POST /api/v1/search
        │
        ▼
┌─────────────────────┐
│ ValidateRequestStep │  Validates required fields, pageSize, offset
└────────┬────────────┘
         │
         ▼
┌──────────────────────┐
│ ConvertRequestStep   │  Maps API request → Google Cloud Retail SearchRequest
└────────┬─────────────┘
         │
         ▼
┌──────────────────┐
│  SearchStep      │  Calls Vertex AI Retail SearchServiceClient
└────────┬─────────┘
         │
         ▼
┌────────────────────────┐
│ ConvertResponseStep    │  Maps Vertex SearchResponse → API response
└────────────────────────┘
```

Each step is a **singleton** registered in DI and wired in sequence inside `Program.cs`.  
The `SearchContext` object flows through the chain carrying the request/response at each stage.

---

## Project Structure

```
backend_dot_net/
├── README.md
└── VertexSearchApi/
    ├── VertexSearchApi.csproj
    ├── Program.cs                          # Entry point, DI wiring, middleware
    ├── appsettings.json                    # Default configuration
    ├── appsettings.Development.json        # Dev-only log levels
    ├── Config/
    │   ├── GcpOptions.cs                   # Strongly-typed GCP config
    │   ├── GcpPaths.cs                     # Builds Retail resource path strings
    │   └── SearchOptions.cs                # Paging, sort & facet aliases
    ├── Controllers/
    │   ├── SearchController.cs             # POST /api/v1/search       (live GCP)
    │   └── MockSearchController.cs         # POST /api/v1/search/mock  (no credentials)
    ├── DTOs/
    │   ├── Request/
    │   │   └── KeywordSearchRequest.cs
    │   └── Response/
    │       ├── KeywordSearchResponse.cs
    │       ├── ProductResult.cs
    │       ├── VariantResult.cs
    │       ├── FacetResult.cs
    │       ├── FacetValueResult.cs
    │       └── SearchStats.cs
    ├── Exceptions/
    │   ├── InvalidSearchRequestException.cs
    │   └── SearchServiceException.cs
    ├── Middleware/
    │   └── GlobalExceptionHandler.cs       # Maps exceptions to HTTP error responses
    └── Services/
        ├── Context/
        │   └── SearchContext.cs            # Mutable pipeline context
        ├── Mappers/
        │   ├── SearchRequestMapper.cs      # API request → Retail SearchRequest
        │   └── SearchResponseMapper.cs     # Retail SearchResponse → API response
        └── Pipeline/
            ├── Step.cs                     # Abstract base with latency logging
            ├── StepService.cs              # Executes the step chain
            └── Search/
                ├── ValidateRequestStep.cs
                ├── ConvertRequestStep.cs
                ├── SearchStep.cs
                └── ConvertResponseStep.cs
```

---

## Quick Start — Mock Mode (no GCP credentials needed)

> Use this if you want to **explore the API schema in Swagger** or develop a UI  
> without setting up Google Cloud authentication.

### 1. Navigate to the project

```bash
cd backend_dot_net/VertexSearchApi
```

### 2. Restore & run (no environment variables required)

```bash
dotnet restore
dotnet run
```

The app starts on `http://localhost:5000`. The mock endpoint is always available  
regardless of GCP configuration.

### 3. Open Swagger UI

```
http://localhost:5000/swagger
```

### 4. Call the mock endpoint

In Swagger, find **`POST /api/v1/search/mock`**, click **Try it out**, paste the  
body below, and hit **Execute**:

```json
{
  "query": "running shoes",
  "visitorId": "user-demo-123",
  "pageSize": 3,
  "offset": 0,
  "facetKeys": ["color", "brand", "size"]
}
```

You will immediately receive a full, realistic response — 5 mock products with  
variants, 4 facets with counts, pagination stats, an attribution token, and  
applied controls.

**No GCP project, no credentials, no Retail API setup required.**

---

## Prerequisites

| Requirement | Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** (or latest preview) |
| [Google Cloud SDK (`gcloud`)](https://cloud.google.com/sdk) | any recent |
| GCP project with **Retail API** enabled | — |

Verify your SDK:

```bash
dotnet --version
# Should print 10.x.x
```

---

## Configuration

All settings live in `appsettings.json`. Override any value via environment variables using the double-underscore separator (`__`), e.g.:

```bash
export Gcp__ProjectId=my-gcp-project
```

### GCP Settings

| Key | Default | Description |
|---|---|---|
| `Gcp:ProjectId` | *(empty)* | **Required.** Your GCP project ID |
| `Gcp:Retail:Location` | `global` | Retail API location |
| `Gcp:Retail:Catalogs` | `default_catalog` | Catalog name |
| `Gcp:Retail:Branch` | `default_branch` | Branch name |
| `Gcp:Retail:Placement` | `default_search` | Serving config / placement |

### Search Defaults

| Key | Default | Description |
|---|---|---|
| `Search:DefaultPageSize` | `20` | Page size when not specified |
| `Search:MaxPageSize` | `100` | Maximum allowed page size |
| `Search:DefaultOffset` | `0` | Pagination offset when not specified |

### Sort Aliases

Client-facing aliases are mapped to Vertex AI `orderBy` expressions:

| Alias | Vertex orderBy |
|---|---|
| `relevance` | *(empty — default relevance)* |
| `price_low_to_high` | `price` |
| `price_high_to_low` | `price desc` |
| `newest` | `attributes.createTime desc` |

### Facet Key Aliases

Client-facing keys are mapped to Vertex attribute names:

| Alias | Vertex attribute |
|---|---|
| `color` | `colorFamilies` |
| `size` | `sizes` |
| `brand` | `brands` |
| `category` | `categories` |
| `price` | `price` |

---

## Google Cloud Authentication

The application uses **Application Default Credentials (ADC)**. Set up one of the following:

### Option 1 — Developer workstation (recommended for local dev)

```bash
gcloud auth application-default login
```

### Option 2 — Service account key file

```bash
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account-key.json
```

### Option 3 — Workload Identity (GKE / Cloud Run)

No additional configuration required when running on GCP infrastructure with the correct service account attached.

> **Required IAM roles on the service account:**
> - `roles/retail.viewer` — read access to Retail API
> - `roles/retail.admin` — if you also manage catalog data

---

## Running the Application (live GCP)

### 1. Clone / navigate to the project

```bash
cd backend_dot_net/VertexSearchApi
```

### 2. Set your GCP project ID

```bash
# via environment variable (recommended)
export Gcp__ProjectId=YOUR_GCP_PROJECT_ID

# OR edit appsettings.json directly
# "Gcp": { "ProjectId": "YOUR_GCP_PROJECT_ID", ... }
```

### 3. Restore packages

```bash
dotnet restore
```

> **Note:** If `Google.Cloud.Retail.V2` version `2.9.0` is unavailable, use the latest:
> ```bash
> dotnet add package Google.Cloud.Retail.V2
> dotnet add package Swashbuckle.AspNetCore
> ```

### 4. Build

```bash
dotnet build
```

### 5. Run

```bash
dotnet run
```

The application starts on `http://localhost:5000` (and `https://localhost:5001`).

```
info: VertexSearchApi[0]
      Warming up SearchServiceClient...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

## Swagger UI

Open your browser and navigate to:

```
http://localhost:5000/swagger
```

You will see the interactive Swagger UI where you can:
- Browse available endpoints
- Inspect request/response schemas
- Execute live API calls directly from the browser

The raw OpenAPI JSON spec is available at:

```
http://localhost:5000/swagger/v1/swagger.json
```

---

## API Reference

### `POST /api/v1/search`

Executes a keyword search against the **live Vertex AI Retail catalog**.  
Requires Google Cloud credentials and a configured GCP project.

> **Getting a `403 PermissionDenied` / quota project error?**  
> Either set up authentication (see [Google Cloud Authentication](#google-cloud-authentication))  
> or use the mock endpoint below while developing.

**Request** (`application/json`):

```json
{
  "query": "red running shoes",
  "visitorId": "user-abc-123",
  "pageSize": 20,
  "offset": 0,
  "orderBy": "price_low_to_high",
  "filter": "availability: IN_STOCK",
  "facetKeys": ["color", "brand", "size"],
  "queryExpansionCondition": "AUTO"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `query` | string | **Yes** | Search query text |
| `visitorId` | string | **Yes** | Unique visitor/session ID for analytics |
| `pageSize` | integer | No | Results per page (default: 20, max: 100) |
| `offset` | integer | No | Pagination offset (default: 0) |
| `orderBy` | string | No | Sort alias: `relevance`, `price_low_to_high`, `price_high_to_low`, `newest` |
| `filter` | string | No | Retail API filter expression |
| `facetKeys` | string[] | No | Facet aliases to return: `color`, `size`, `brand`, `category`, `price` |
| `queryExpansionCondition` | string | No | `AUTO` (default) or `DISABLED` |

**Response** (`200 OK`):

```json
{
  "products": [
    {
      "id": "product-001",
      "title": "Red Running Shoes",
      "categories": ["Footwear > Running"],
      "uri": "https://example.com/products/001",
      "attributes": {
        "color": "Red",
        "brand": "Nike"
      },
      "variants": [
        { "id": "variant-001-s10" }
      ]
    }
  ],
  "facets": [
    {
      "key": "colorFamilies",
      "values": [
        { "value": "Red", "count": 42 },
        { "value": "Blue", "count": 18 }
      ]
    }
  ],
  "stats": {
    "returned": 20,
    "totalResults": 157,
    "offset": 0
  },
  "correctedQuery": "red running shoes",
  "attributionToken": "AbCdEfGh...",
  "appliedControls": ["boost-new-arrivals"]
}
```

**Error responses:**

| Status | Condition |
|---|---|
| `400` | Missing `query` or `visitorId`, invalid `pageSize`/`offset`, malformed JSON |
| `401` | Invalid/missing GCP credentials |
| `403` | Service account lacks required IAM roles |
| `404` | Catalog or branch not found |
| `429` | Retail API quota exceeded |
| `503` | Vertex AI Retail service unavailable |

---

### `POST /api/v1/search/mock`

Returns **hardcoded dummy results** using the exact same request/response schema as  
the live endpoint. Implemented in [Controllers/MockSearchController.cs](VertexSearchApi/Controllers/MockSearchController.cs).  
No GCP credentials or Retail API access required.

**Request** (`application/json`) — same schema as the live endpoint:

```json
{
  "query": "running shoes",
  "visitorId": "user-demo-123",
  "pageSize": 3,
  "offset": 0,
  "orderBy": "price_low_to_high",
  "facetKeys": ["color", "brand", "size"],
  "queryExpansionCondition": "AUTO"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `query` | string | **Yes** | Any non-empty string |
| `visitorId` | string | **Yes** | Any non-empty string |
| `pageSize` | integer | No | Slices the mock product list (default: 20, clamped to 1–100) |
| `offset` | integer | No | Skips N items from the mock list (default: 0) |
| `orderBy` | string | No | Accepted but not applied to mock data |
| `filter` | string | No | Accepted but not applied to mock data |
| `facetKeys` | string[] | No | Filters which of the 4 mock facets are returned |
| `queryExpansionCondition` | string | No | Accepted but ignored |

**Mock data included in every response:**

| Data | Details |
|---|---|
| Products | 5 items: trail shoes (Nike/Red), road shoes (Adidas/Blue), road shoes (New Balance/Black), kids shoes (Puma/Green), marathon shoes (Asics/White) |
| Facets | `colorFamilies` (5 values), `brands` (5 values), `sizes` (6 values), `categories` (5 values) |
| Stats | `returned` reflects actual page, `totalResults` is always 5 |
| `attributionToken` | A random UUID prefixed with `mock-token-` |
| `appliedControls` | Always `["boost-new-arrivals", "pin-sale-items"]` |
| `correctedQuery` | Populated only when query contains `"shoez"` (demo of spell correction) |

**Example response** (`pageSize: 2, offset: 0, facetKeys: ["color", "brand"]`):

```json
{
  "products": [
    {
      "id": "product-001",
      "title": "Men's Trail Running Shoes — Red",
      "categories": ["Footwear", "Footwear > Running", "Footwear > Running > Trail"],
      "uri": "https://example.com/products/001",
      "attributes": { "color": "Red", "brand": "Nike", "gender": "Men", "material": "Mesh" },
      "variants": [
        { "id": "product-001-sz9" },
        { "id": "product-001-sz10" },
        { "id": "product-001-sz11" }
      ]
    },
    {
      "id": "product-002",
      "title": "Women's Lightweight Running Shoes — Blue",
      "categories": ["Footwear", "Footwear > Running"],
      "uri": "https://example.com/products/002",
      "attributes": { "color": "Blue", "brand": "Adidas", "gender": "Women" },
      "variants": [
        { "id": "product-002-sz7" },
        { "id": "product-002-sz8" }
      ]
    }
  ],
  "facets": [
    {
      "key": "colorFamilies",
      "values": [
        { "value": "Red",   "count": 42 },
        { "value": "Blue",  "count": 35 },
        { "value": "Black", "count": 28 },
        { "value": "White", "count": 19 },
        { "value": "Green", "count": 11 }
      ]
    },
    {
      "key": "brands",
      "values": [
        { "value": "Nike",        "count": 58 },
        { "value": "Adidas",      "count": 47 },
        { "value": "New Balance", "count": 32 },
        { "value": "Asics",       "count": 24 },
        { "value": "Puma",        "count": 18 }
      ]
    }
  ],
  "stats": { "returned": 2, "totalResults": 5, "offset": 0 },
  "attributionToken": "mock-token-3f2a1b...",
  "appliedControls": ["boost-new-arrivals", "pin-sale-items"]
}
```

**Error responses:**

| Status | Condition |
|---|---|
| `400` | Missing `query` or `visitorId` |

---

## Package Versions & Compatibility

| Package | Version used | Notes |
|---|---|---|
| `Google.Cloud.Retail.V2` | `2.9.0` | Update to latest via `dotnet add package Google.Cloud.Retail.V2` |
| `Swashbuckle.AspNetCore` | `7.2.0` | Provides Swagger UI; compatible with .NET 9/10 |

### .NET 10 Preview / Stable

If you are on a .NET 10 **preview** SDK and encounter package compatibility issues, you can either:

- **Target net9.0** temporarily by changing `<TargetFramework>net9.0</TargetFramework>` in the `.csproj` — all code is compatible.
- Or switch to the built-in OpenAPI + Scalar UI approach (available natively in .NET 9/10):
  ```bash
  dotnet add package Scalar.AspNetCore
  ```
  Then in `Program.cs`, replace the Swashbuckle lines with:
  ```csharp
  builder.Services.AddOpenApi();
  // ...
  app.MapOpenApi();
  app.MapScalarApiReference();
  ```
  Scalar UI will be available at `http://localhost:5000/scalar/v1`.
