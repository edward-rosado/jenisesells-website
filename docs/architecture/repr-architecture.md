# REPR Architecture

## Overview

REPR stands for **Request → Endpoint → Response**.

It is an architectural pattern used for designing API endpoints where
each endpoint is implemented as a self-contained unit composed of three
components:

1.  Request -- the input contract
2.  Endpoint -- the execution logic
3.  Response -- the output contract

Instead of organizing APIs around controllers or services, REPR
organizes code around individual endpoint behaviors.

Each endpoint represents a single use case.

This leads to systems where:

-   endpoints are small and focused
-   input and output contracts are explicit
-   logic is easy to locate
-   APIs scale without controllers becoming large and difficult to
    maintain

------------------------------------------------------------------------

# Motivation

Traditional APIs often rely on MVC controllers where multiple endpoints
live inside a single class.

Example:

    ListingsController
      CreateListing()
      UpdateListing()
      DeleteListing()
      GetListing()
      SearchListings()
      GetListingAnalytics()

Over time these controllers accumulate responsibilities and become
difficult to maintain. This is often referred to as the Fat Controller
Problem.

REPR solves this by defining one endpoint per use case.

Each endpoint becomes its own small module with a clear responsibility.

------------------------------------------------------------------------

# Core Components

## Request

The Request defines the input contract for an endpoint.

It represents the data expected from the client and typically maps to:

-   HTTP body
-   query parameters
-   route parameters
-   headers

Example:

``` csharp
public class CreateListingRequest
{
    public string Address { get; set; } = default!;
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
}
```

Responsibilities:

-   define the expected input structure
-   support serialization and validation
-   provide strong typing

Request objects should not contain business logic.

------------------------------------------------------------------------

## Endpoint

The Endpoint contains the execution logic for handling the request.

It is responsible for:

-   coordinating domain logic
-   interacting with services or repositories
-   creating the response

Example:

``` csharp
public class CreateListingEndpoint
{
    private readonly ListingService _listingService;

    public CreateListingEndpoint(ListingService listingService)
    {
        _listingService = listingService;
    }

    public async Task<CreateListingResponse> Handle(CreateListingRequest request)
    {
        var listing = await _listingService.CreateAsync(request);

        return new CreateListingResponse
        {
            Id = listing.Id,
            Address = listing.Address,
            Price = listing.Price
        };
    }
}
```

Endpoints should remain thin orchestration layers.

Business rules should live in the domain layer or services.

------------------------------------------------------------------------

## Response

The Response defines the output contract returned to the client.

It determines what data the API exposes.

Example:

``` csharp
public class CreateListingResponse
{
    public Guid Id { get; set; }
    public string Address { get; set; } = default!;
    public decimal Price { get; set; }
}
```

Responsibilities:

-   shape API output
-   prevent exposing internal domain models
-   define stable API contracts

------------------------------------------------------------------------

# Request Flow

The lifecycle of a request typically follows this pipeline:

    HTTP Request
         │
         ▼
    Request Model
         │
         ▼
    Endpoint Logic
         │
         ▼
    Domain / Services / Database
         │
         ▼
    Response Model
         │
         ▼
    HTTP Response

Each endpoint therefore represents a complete vertical slice of
functionality.

------------------------------------------------------------------------

# Directory Structure

A typical REPR project organizes endpoints by feature.

Example:

    src/
      Api/
        Listings/
          Create/
            CreateListingRequest.cs
            CreateListingEndpoint.cs
            CreateListingResponse.cs

          Get/
            GetListingRequest.cs
            GetListingEndpoint.cs
            GetListingResponse.cs

        Users/
          Login/
            LoginRequest.cs
            LoginEndpoint.cs
            LoginResponse.cs

All code related to a single endpoint is co-located.

This improves discoverability and reduces cognitive overhead.

------------------------------------------------------------------------

# REPR with ASP.NET Minimal APIs

Modern .NET APIs often expose REPR endpoints using Minimal APIs.

Example:

``` csharp
app.MapPost("/api/listings", async (
    CreateListingRequest request,
    CreateListingEndpoint endpoint) =>
{
    var response = await endpoint.Handle(request);
    return Results.Ok(response);
});
```

This keeps HTTP routing thin while delegating logic to the endpoint
class.

------------------------------------------------------------------------

# REPR vs MVC

  -----------------------------------------------------------------------
  Aspect                  MVC                     REPR
  ----------------------- ----------------------- -----------------------
  Organization            Controllers with many   One endpoint per use
                          methods                 case

  Complexity              Controllers grow over   Small focused modules
                          time                    

  Testability             Harder to isolate logic Easy to test endpoints

  Discoverability         Logic scattered         Endpoint code
                                                  co-located

  Scaling                 Controllers become      Endpoints scale
                          large                   independently
  -----------------------------------------------------------------------

MVC works well for UI applications but is less ideal for API-only
services.

REPR aligns directly with the request-response nature of HTTP APIs.

------------------------------------------------------------------------

# Relationship to Vertical Slice Architecture

REPR works naturally with Vertical Slice Architecture.

Each feature slice contains:

    Feature
     ├ Request
     ├ Endpoint
     ├ Response
     ├ Domain Logic
     └ Persistence

This organizes the system around use cases instead of technical layers.

------------------------------------------------------------------------

# Relationship to CQRS

REPR also aligns well with Command Query Responsibility Segregation
(CQRS).

Commands:

    CreateListing
    UpdateListing
    DeleteListing

Queries:

    GetListing
    SearchListings
    GetListingStats

Each command or query becomes its own endpoint.

------------------------------------------------------------------------

# Advantages

## Simplicity

The architecture follows a simple mental model:

    Input → Logic → Output

There are minimal abstractions.

------------------------------------------------------------------------

## Maintainability

Endpoints are isolated units.

Changes to one endpoint rarely affect others.

------------------------------------------------------------------------

## Testability

Endpoints can be tested independently.

Example:

``` csharp
[Fact]
public async Task CreateListing_ReturnsListing()
{
    var service = new ListingService();
    var endpoint = new CreateListingEndpoint(service);

    var request = new CreateListingRequest
    {
        Address = "123 Main St",
        Price = 500000,
        Bedrooms = 3
    };

    var response = await endpoint.Handle(request);

    Assert.NotEqual(Guid.Empty, response.Id);
}
```

------------------------------------------------------------------------

# Best Practices

Keep endpoints thin.

Endpoints should orchestrate logic rather than implement domain rules.

Use explicit contracts.

Avoid exposing domain models directly through the API.

Group endpoints by feature rather than technical layers.

------------------------------------------------------------------------

# Summary

REPR is a lightweight architecture for building APIs that:

-   organizes code by endpoint
-   separates input, logic, and output
-   improves maintainability
-   scales cleanly as APIs grow

By structuring APIs around use-case driven endpoints, REPR keeps systems
simple, discoverable, and resilient as they evolve.
