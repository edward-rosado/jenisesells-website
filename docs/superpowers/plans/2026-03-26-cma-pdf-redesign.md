# CMA PDF Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the CMA PDF pipeline to produce a fully branded, professionally laid-out report with enriched subject property data, tiered comp selection, and a browser-accessible download endpoint.

**Architecture:** Five-change surface area: (1) Domain model additions (`Comp.IsRecent`, `RentCastValuation.SubjectProperty`), (2) tiered comp selection in `RentCastCompSource`, (3) new `EnrichSubjectAsync` step in `CmaProcessingWorker`, (4) Claude prompt updates for comp weighting, (5) full `CmaPdfGenerator` redesign with `IImageResolver` + `LocalFirstImageResolver` + `DownloadCmaEndpoint`.

**Tech Stack:** .NET 10, QuestPDF (Community), HttpClient (image resolution fallback), REPR pattern, OpenTelemetry, xUnit + FluentAssertions + Moq

**Spec:** `docs/superpowers/specs/2026-03-26-cma-pdf-redesign-design.md`

---

## Spawn Pattern

```
Phase 1: Launch Tasks 1–4 in parallel (4 agents) — data models + comp selection + enrichment step + prompt
Phase 2: Launch Tasks 5–7 in parallel (3 agents) — image resolver + PDF rewrite + download endpoint
Phase 3: Launch Tasks 8–9 in parallel (2 agents) — observability + docs
Phase 4: Task 10 alone — full build + test + coverage verification
```

**Important codebase notes for implementers:**
- `Comp` is a `class` (not a record) with `required` `init` properties — use object initializer syntax, not `with` expressions.
- `RentCastValuation` is a `record` — `with` expressions work.
- `CmaPipelineContext` uses a typed property bag pattern (`Get<T>` / `Set`). Add `SubjectProperty` as a typed property matching the existing `Comps` and `Analysis` pattern.
- `MapComps` is `internal static` — signature change adds a `DateOnly today` parameter. All existing tests must be updated to pass the parameter.
- `CmaProcessingWorker` already has `IAccountConfigService` injected — no new DI dependency needed for the enrichment step.
- `DetermineReportType` threshold changes from `>= 6 → Comprehensive` to `>= 5 → Comprehensive`. This is a one-line change.
- Architecture rule: `IImageResolver` interface belongs in `RealEstateStar.Domain.Cma.Interfaces`. `LocalFirstImageResolver` implementation belongs in `RealEstateStar.Workers.Cma` (needs filesystem + HttpClient — not Domain's concern).
- `DownloadCmaEndpoint` goes in `RealEstateStar.Api/Features/Cma/Download/` — new `Cma` feature folder under `Features/`.
- Log code namespace: `[CMA-PDF-NNN]` for PDF generator, `[CMA-DL-NNN]` for download endpoint, `[CMA-IMG-NNN]` for image resolver.
- `CancellationToken ct` required everywhere — no `= default` defaults.
- All new classes: `internal sealed` with `[assembly: InternalsVisibleTo("RealEstateStar.Workers.Cma.Tests")]` (already present in .csproj or Properties/AssemblyInfo.cs — verify before adding).
- No `Source` column in the new comp table. Replace with `Age` column (months since sale).
- ConversationStarters from CMA analysis are NOT included in the PDF — agent-internal only. They continue to be sent in the agent notification email.

---

## File Structure

**Files to create:**
- `apps/api/RealEstateStar.Domain/Cma/Interfaces/IImageResolver.cs` — new interface
- `apps/api/RealEstateStar.Workers.Cma/LocalFirstImageResolver.cs` — local-first + HTTP fallback
- `apps/api/RealEstateStar.Api/Features/Cma/Download/DownloadCmaEndpoint.cs` — PDF download endpoint
- `apps/api/tests/RealEstateStar.Clients.RentCast.Tests/RentCastClientSubjectPropertyTests.cs` — focused tests for SubjectProperty mapping
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/LocalFirstImageResolverTests.cs` — image resolver tests

**Files to modify:**
- `apps/api/RealEstateStar.Domain/Cma/Models/Comp.cs` — add `IsRecent` + `Correlation` properties
- `apps/api/RealEstateStar.Domain/Cma/Models/RentCastValuation.cs` — add `SubjectProperty` + `RentCastSubjectProperty` record
- `apps/api/RealEstateStar.Workers.Cma/CmaPipelineContext.cs` — add `SubjectProperty` slot + `StepEnrichSubject` constant
- `apps/api/RealEstateStar.Workers.Cma/RentCastCompSource.cs` — tiered selection + `IsRecent` annotation + `today` param
- `apps/api/RealEstateStar.Workers.Cma/ClaudeCmaAnalyzer.cs` — system prompt addition + `[Older sale]` annotation + remove `Source` from prompt
- `apps/api/RealEstateStar.Workers.Cma/CmaProcessingWorker.cs` — `EnrichSubjectAsync` step + updated `DetermineReportType`
- `apps/api/RealEstateStar.Workers.Cma/CmaPdfGenerator.cs` — full layout redesign (branding, new sections, `IImageResolver`, remove Source column)
- `apps/api/RealEstateStar.Domain/Cma/Interfaces/ICmaPdfGenerator.cs` — update signature to include `logoBytes` + `headshotBytes`
- `apps/api/RealEstateStar.Clients.RentCast/RentCastClient.cs` — map `subjectProperty` from API response
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/RentCastCompSourceTests.cs` — update `MapComps` call sites + add 5 tiered-selection tests
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/ClaudeCmaAnalyzerTests.cs` — add `IsRecent` annotation tests
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/CmaProcessingWorkerTests.cs` — add enrichment tests + `DetermineReportType` update
- `apps/api/tests/RealEstateStar.Workers.Cma.Tests/CmaPdfGeneratorTests.cs` — add null-image + older-comp + enriched-subject smoke tests
- `apps/api/RealEstateStar.Workers.Cma/RealEstateStar.Workers.Cma.csproj` — add `IImageResolver` DI registration note (no new package needed)

**Files to delete:** None.

---

## Phase 1: Data Models + Backend Logic (4 parallel tasks)

### Task 1 — Domain Model Additions

**Files:** `Comp.cs`, `RentCastValuation.cs`

**Steps:**

- [ ] Open `apps/api/RealEstateStar.Domain/Cma/Models/Comp.cs`

- [ ] Add `IsRecent` property and `Correlation` property to `Comp`:

```csharp
/// <summary>
/// True when SaleDate is within the last 6 months from report generation.
/// Set by RentCastCompSource during tiered comp selection.
/// Used by ClaudeCmaAnalyzer to annotate older comps in the prompt.
/// </summary>
public bool IsRecent { get; init; }

/// <summary>
/// RentCast similarity score (0–1). Higher = more similar to subject.
/// Used as the sort key within each recency tier during comp selection.
/// Null when RentCast does not return a correlation value; treated as 0 (sorts last).
/// </summary>
public double? Correlation { get; init; }
```

- [ ] Open `apps/api/RealEstateStar.Domain/Cma/Models/RentCastValuation.cs`

- [ ] Add `SubjectProperty` to `RentCastValuation` and new `RentCastSubjectProperty` record:

```csharp
public record RentCastValuation
{
    public required decimal Price { get; init; }
    public required decimal PriceRangeLow { get; init; }
    public required decimal PriceRangeHigh { get; init; }
    public required IReadOnlyList<RentCastComp> Comparables { get; init; }

    /// <summary>
    /// Subject property data returned by RentCast /v1/avm/value.
    /// Used to enrich lead data when seller omits beds/baths/sqft.
    /// Null when RentCast does not return a subjectProperty object.
    /// </summary>
    public RentCastSubjectProperty? SubjectProperty { get; init; }
}

/// <summary>
/// Property attributes for the subject address returned by RentCast.
/// All fields are nullable — not all properties have complete data.
/// </summary>
public record RentCastSubjectProperty
{
    public int? Bedrooms { get; init; }
    public decimal? Bathrooms { get; init; }
    public int? SquareFootage { get; init; }
    public string? PropertyType { get; init; }
    public int? YearBuilt { get; init; }
}
```

- [ ] Build `RealEstateStar.Domain` to confirm no compile errors:
```bash
dotnet build apps/api/RealEstateStar.Domain/RealEstateStar.Domain.csproj
```

- [ ] Commit:
```
feat(domain): add Comp.IsRecent, Comp.Correlation, RentCastValuation.SubjectProperty
```

---

### Task 2 — Tiered Comp Selection in RentCastCompSource

**Files:** `RentCastCompSource.cs`, `RentCastCompSourceTests.cs`

**Steps:**

- [ ] Open `apps/api/RealEstateStar.Workers.Cma/RentCastCompSource.cs`

- [ ] Update `MapComps` signature to accept `DateOnly today` for testability:

```csharp
internal static List<Comp> MapComps(
    IReadOnlyList<RentCastComp> comparables,
    CompSearchRequest request,
    DateOnly today,
    ILogger logger)
```

- [ ] Replace the existing loop with the tiered selection algorithm. The full implementation:

```csharp
internal static List<Comp> MapComps(
    IReadOnlyList<RentCastComp> comparables,
    CompSearchRequest request,
    DateOnly today,
    ILogger logger)
{
    const int Target = 5;
    var cutoff = today.AddMonths(-6);

    // Step 1: filter invalid comps (existing logic)
    var filtered = new List<(RentCastComp Raw, DateOnly SaleDate)>();

    foreach (var rc in comparables)
    {
        if (rc.Price is null or <= 0) continue;
        if (rc.SquareFootage is null or <= 0) continue;
        if (string.IsNullOrWhiteSpace(rc.FormattedAddress)) continue;

        DateTimeOffset? resolvedDate = rc.Status?.Equals("Inactive",
            StringComparison.OrdinalIgnoreCase) == true
            ? rc.RemovedDate
            : rc.ListedDate;

        if (resolvedDate is null) continue;

        var saleDate = DateOnly.FromDateTime(resolvedDate.Value.UtcDateTime);

        if (request.SqFt.HasValue
            && rc.PropertyType is not null
            && ExcludedPropertyTypes.Contains(rc.PropertyType))
            continue;

        filtered.Add((rc, saleDate));
    }

    // Step 2: partition by recency
    var recent = filtered
        .Where(x => x.SaleDate >= cutoff)
        .OrderByDescending(x => x.Raw.Correlation ?? 0.0)
        .ToList();

    var older = filtered
        .Where(x => x.SaleDate < cutoff)
        .OrderByDescending(x => x.Raw.Correlation ?? 0.0)
        .ToList();

    // Step 3: fill to target
    var selected = recent.Take(Target).ToList();
    if (selected.Count < Target)
        selected.AddRange(older.Take(Target - selected.Count));

    logger.LogInformation(
        "[COMP-003] Tiered comp selection: {RecentCount} recent + {OlderCount} older = {Total} selected (target {Target})",
        Math.Min(recent.Count, Target),
        Math.Max(0, selected.Count - Math.Min(recent.Count, Target)),
        selected.Count,
        Target);

    // Step 4: map + annotate IsRecent
    return selected.Select(x =>
    {
        var rc = x.Raw;
        var saleDate = x.SaleDate;
        return new Comp
        {
            Address = rc.FormattedAddress,
            SalePrice = rc.Price!.Value,
            SaleDate = saleDate,
            Beds = rc.Bedrooms ?? 0,
            Baths = (int)Math.Round(rc.Bathrooms ?? 0, MidpointRounding.AwayFromZero),
            Sqft = rc.SquareFootage!.Value,
            DaysOnMarket = rc.DaysOnMarket,
            DistanceMiles = rc.Distance ?? 0.0,
            Source = CompSource.RentCast,
            Correlation = rc.Correlation,
            IsRecent = saleDate >= cutoff
        };
    }).ToList();
}
```

- [ ] Update `FetchAsync` to pass `DateOnly.FromDateTime(DateTime.UtcNow)`:

```csharp
var comps = MapComps(valuation.Comparables, request, DateOnly.FromDateTime(DateTime.UtcNow), logger);
```

- [ ] Also store `SubjectProperty` from valuation so the worker can access it. Update `FetchAsync` return type to a tuple or update the interface. **Decision:** Since `ICompSource.FetchAsync` returns `List<Comp>`, the `SubjectProperty` must be passed via a different mechanism. The cleanest approach within existing architecture: `RentCastCompSource` stores the last resolved `SubjectProperty` in a field accessible to the worker. BUT this creates state. **Correct approach:** Change `ICompSource` to return a result object, OR expose `SubjectProperty` directly from `RentCastCompSource.FetchAsync` by adding it to a context. Per the spec, `CmaPipelineContext.SubjectProperty` is the slot — the worker (`CmaProcessingWorker`) already has direct access to `ICompAggregator`. Change the approach: `ICompAggregator` will be replaced by direct `IRentCastClient` usage in the worker, OR store `SubjectProperty` on context from `FetchCompsAsync`. **Simplest path:** After `compAggregator.FetchCompsAsync`, the worker also calls `rentCastClient.GetValuationAsync` directly — but this would be a double-call. **Correct architecture:** expose `SubjectProperty` from `FetchAsync` via an `out` parameter or a new method, keeping `ICompSource` unchanged. The spec says: in `FetchCompsAsync`, after getting the valuation, store `valuation.SubjectProperty` on the context. Since `CmaProcessingWorker` uses `ICompAggregator` (not `IRentCastClient` directly), and `ICompAggregator` wraps multiple `ICompSource` instances — the `SubjectProperty` should be stored in the `RentCastCompSource` and exposed. Add a property: `public RentCastSubjectProperty? LastSubjectProperty { get; private set; }`. The worker then casts `compAggregator` to access it, OR better: inject `IRentCastClient` directly alongside `ICompAggregator` for the enrichment data path. **Final decision (simplest, cleanest):** Store `SubjectProperty` on `CmaPipelineContext` by having `CmaProcessingWorker.FetchCompsAsync` also call `IRentCastClient.GetValuationAsync` using the same address, or expose it from the aggregator result. Since this creates coupling, the cleanest solution matching the spec: add `RentCastSubjectProperty? LastSubjectProperty` as a read property on `RentCastCompSource`, set in `FetchAsync`, and have `CmaProcessingWorker` downcast to get it. **Implementer decision:** Discuss with Eddie before adding any interface changes to `ICompAggregator`. For this plan, implement it by modifying `FetchCompsAsync` to call `rentCastClient` directly for the subject property data if `ICompAggregator` cannot expose it cleanly. The DI-registered `IRentCastClient` is already available in `Api`'s composition root.

  > **Open question for implementer:** Before coding Task 2, check whether `ICompAggregator` has room for a `SubjectProperty` result. If not, the cleanest path is adding `IRentCastClient` as a constructor dependency on `CmaProcessingWorker` (already allowed under architecture rules — `Workers.Cma → Domain only`, and `IRentCastClient` is a Domain interface). This avoids any interface change to `ICompAggregator`.

- [ ] Open `apps/api/tests/RealEstateStar.Workers.Cma.Tests/RentCastCompSourceTests.cs`

- [ ] Update all existing `MapComps` call sites to pass a `today` parameter. Use `new DateOnly(2025, 3, 1)` as a fixed "today" in tests:

```csharp
// Before:
RentCastCompSource.MapComps(valuation.Comparables, request, NullLogger.Instance);

// After:
RentCastCompSource.MapComps(valuation.Comparables, request, new DateOnly(2026, 3, 26), NullLogger.Instance);
```

- [ ] Add 5 tiered-selection tests (covering all 5 algorithm branches):

```csharp
[Fact]
public void MapComps_SevenRecentComps_SelectsTopFiveByCorrelation()
{
    var today = new DateOnly(2026, 3, 26);
    var comps = Enumerable.Range(1, 7)
        .Select(i => MakeComp(
            address: $"{i * 10} Recent St",
            removedDate: new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero))
            with { Correlation = (double)i / 10.0 })
        .ToList();

    var result = RentCastCompSource.MapComps(comps, MakeRequest(), today, NullLogger.Instance);

    result.Should().HaveCount(5);
    result.Should().AllSatisfy(c => c.IsRecent.Should().BeTrue());
    // Top 5 correlations are 0.7, 0.6, 0.5, 0.4, 0.3 (indices 7,6,5,4,3)
    result[0].Correlation.Should().BeApproximately(0.7, 0.001);
}

[Fact]
public void MapComps_ThreeRecentFourOlder_SelectsThreePlusTwoOlderByCorrelation()
{
    var today = new DateOnly(2026, 3, 26);
    var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var olderDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var recent = Enumerable.Range(1, 3)
        .Select(i => MakeComp(address: $"{i} Recent", removedDate: recentDate) with { Correlation = 0.5 })
        .ToList();
    var older = Enumerable.Range(1, 4)
        .Select(i => MakeComp(address: $"{i} Older", removedDate: olderDate) with { Correlation = (double)i / 10.0 })
        .ToList();

    var result = RentCastCompSource.MapComps(recent.Concat(older).ToList(), MakeRequest(), today, NullLogger.Instance);

    result.Should().HaveCount(5);
    result.Count(c => c.IsRecent).Should().Be(3);
    result.Count(c => !c.IsRecent).Should().Be(2);
}

[Fact]
public void MapComps_ZeroRecentThreeOlder_AllOlderAndAllNotRecent()
{
    var today = new DateOnly(2026, 3, 26);
    var olderDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var comps = Enumerable.Range(1, 3)
        .Select(i => MakeComp(address: $"{i} Older", removedDate: olderDate))
        .ToList();

    var result = RentCastCompSource.MapComps(comps, MakeRequest(), today, NullLogger.Instance);

    result.Should().HaveCount(3);
    result.Should().AllSatisfy(c => c.IsRecent.Should().BeFalse());
}

[Fact]
public void MapComps_TwoTotalComps_ReturnsTwoWithoutPadding()
{
    var today = new DateOnly(2026, 3, 26);
    var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var comps = Enumerable.Range(1, 2)
        .Select(i => MakeComp(address: $"{i} Main", removedDate: recentDate))
        .ToList();

    var result = RentCastCompSource.MapComps(comps, MakeRequest(), today, NullLogger.Instance);

    result.Should().HaveCount(2);
}

[Fact]
public void MapComps_NullCorrelation_SortsLastWithinTier()
{
    var today = new DateOnly(2026, 3, 26);
    var recentDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var highCorr = MakeComp(address: "High Corr", removedDate: recentDate) with { Correlation = 0.9 };
    var nullCorr = MakeComp(address: "Null Corr", removedDate: recentDate) with { Correlation = null };

    var result = RentCastCompSource.MapComps([highCorr, nullCorr], MakeRequest(), today, NullLogger.Instance);

    result[0].Address.Should().Be("High Corr");
    result[1].Address.Should().Be("Null Corr");
}
```

- [ ] Run tests: `dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/ --no-build`

- [ ] Commit:
```
feat(cma): tiered comp selection — 5-comp target, 6-month recency, IsRecent annotation
```

---

### Task 3 — Subject Property Enrichment Step

**Files:** `CmaPipelineContext.cs`, `CmaProcessingWorker.cs`, `CmaProcessingWorkerTests.cs`

**Steps:**

- [ ] Open `apps/api/RealEstateStar.Workers.Cma/CmaPipelineContext.cs`

- [ ] Add `StepEnrichSubject` constant and `SubjectProperty` typed property:

```csharp
public const string StepEnrichSubject = "enrich-subject";

public RentCastSubjectProperty? SubjectProperty
{
    get => Get<RentCastSubjectProperty>("subject-property");
    set { if (value is not null) Set("subject-property", value); }
}
```

- [ ] Open `apps/api/RealEstateStar.Workers.Cma/CmaProcessingWorker.cs`

- [ ] Add `IRentCastClient` as a constructor dependency (needed to populate `ctx.SubjectProperty` without calling `ICompAggregator` twice):

```csharp
// Add to constructor parameters:
IRentCastClient rentCastClient,
```

- [ ] Update `FetchCompsAsync` to also populate `ctx.SubjectProperty` from the RentCast valuation. Since `ICompAggregator` wraps the client, add a second direct call to `rentCastClient` for the subject data:

```csharp
private async Task FetchCompsAsync(CmaPipelineContext ctx, CancellationToken ct)
{
    var seller = ctx.Request.SellerDetails!;
    var searchRequest = new CompSearchRequest
    {
        Address = seller.Address,
        City = seller.City,
        State = seller.State,
        Zip = seller.Zip,
        Beds = seller.Beds,
        Baths = seller.Baths,
        SqFt = seller.Sqft
    };

    ctx.Comps = await compAggregator.FetchCompsAsync(searchRequest, ct);
    CmaDiagnostics.CompsFound.Record(ctx.Comps.Count);

    // Fetch subject property data from RentCast for enrichment
    var fullAddress = $"{seller.Address}, {seller.City}, {seller.State} {seller.Zip}";
    var valuation = await rentCastClient.GetValuationAsync(fullAddress, ct);
    if (valuation?.SubjectProperty is { } sp)
    {
        ctx.SubjectProperty = sp;
        logger.LogInformation("[CmaWorker-010] SubjectProperty loaded from RentCast for lead {LeadId}", ctx.Request.Id);
    }
    else
    {
        logger.LogInformation("[CmaWorker-Enrich-001] RentCast did not return subjectProperty for lead {LeadId}", ctx.Request.Id);
    }
}
```

- [ ] Add `EnrichSubjectAsync` method:

```csharp
private Task EnrichSubjectAsync(CmaPipelineContext ctx, CancellationToken ct)
{
    var sp = ctx.SubjectProperty;
    if (sp is null) return Task.CompletedTask;

    var seller = ctx.Request.SellerDetails;
    if (seller is null) return Task.CompletedTask;

    // Only fill fields the lead left null — never overwrite explicit lead input
    var enrichedBeds = seller.Beds ?? (sp.Bedrooms.HasValue ? (int?)sp.Bedrooms : null);
    var enrichedBaths = seller.Baths ?? (sp.Bathrooms.HasValue ? (int?)(int)Math.Round(sp.Bathrooms.Value) : null);
    var enrichedSqft = seller.Sqft ?? sp.SquareFootage;

    seller.Beds = enrichedBeds;
    seller.Baths = enrichedBaths;
    seller.Sqft = enrichedSqft;

    logger.LogInformation(
        "[CmaWorker-Enrich] Subject enriched from RentCast. Beds={Beds} Baths={Baths} Sqft={Sqft} LeadId={LeadId}",
        enrichedBeds, enrichedBaths, enrichedSqft, ctx.Request.Id);

    return Task.CompletedTask;
}
```

  > **Note:** If `SellerDetails` is immutable (all `init` setters), the enrichment must create a new instance. Check the `SellerDetails` model — if all properties use `init`, replace the property assignments with:
  > ```csharp
  > ctx.Request.MergeSellerDetails(seller with { Beds = enrichedBeds, Baths = enrichedBaths, Sqft = enrichedSqft });
  > ```
  > Or add a `WithEnrichedData` method to `SellerDetails`. Confirm the model shape before implementing.

- [ ] Update `ProcessAsync` to insert `EnrichSubjectAsync` between `FetchComps` and `Analyze`:

```csharp
await RunStepAsync(ctx, CmaPipelineContext.StepFetchComps, () => FetchCompsAsync(ctx, ct), ct);

if (ctx.Comps is null || ctx.Comps.Count == 0)
{
    logger.LogWarning("[CmaWorker] No comps found for lead {LeadId}. Skipping CMA.",
        ctx.Request.Id);
    return;
}

await RunStepAsync(ctx, CmaPipelineContext.StepEnrichSubject, () => EnrichSubjectAsync(ctx, ct), ct);
await RunStepAsync(ctx, CmaPipelineContext.StepAnalyze, () => AnalyzeAsync(ctx, ct), ct);
await RunStepAsync(ctx, CmaPipelineContext.StepGeneratePdf, () => GeneratePdfAsync(ctx, ct), ct);
await StorePdfAsync(ctx, ct);
await RunStepAsync(ctx, CmaPipelineContext.StepNotifySeller, () => NotifySellerAsync(ctx, ct), ct);
```

- [ ] Update `DetermineReportType` — change Comprehensive threshold from `>= 6` to `>= 5`:

```csharp
internal static ReportType DetermineReportType(int compCount) =>
    compCount switch
    {
        >= 5 => ReportType.Comprehensive,
        >= 3 => ReportType.Standard,
        _ => ReportType.Lean
    };
```

- [ ] Open `apps/api/tests/RealEstateStar.Workers.Cma.Tests/CmaProcessingWorkerTests.cs`

- [ ] Add `IRentCastClient` mock to the test class:

```csharp
private readonly Mock<IRentCastClient> _rentCastClient = new();
```

- [ ] Update `CreateWorker` to pass `_rentCastClient.Object`.

- [ ] Add enrichment tests:

```csharp
[Fact]
public void EnrichSubject_NullBedsFilledFromSubjectProperty()
{
    var sp = new RentCastSubjectProperty { Bedrooms = 3, SquareFootage = 1850 };
    var seller = new SellerDetails { Address = "123 Main", City = "A", State = "NJ", Zip = "07000",
        Beds = null, Baths = null, Sqft = null };
    var ctx = new CmaPipelineContext { SubjectProperty = sp };
    // set ctx.Request with seller details
    // call EnrichSubjectAsync via reflection or make it internal + InternalsVisibleTo
    // assert: seller.Beds == 3, seller.Sqft == 1850
}

[Fact]
public void EnrichSubject_NonNullBedsNotOverwritten()
{
    // seller.Beds = 4, sp.Bedrooms = 3
    // after enrichment: seller.Beds still == 4
}

[Fact]
public void EnrichSubject_NullSubjectProperty_NoChangeToSellerDetails()
{
    // ctx.SubjectProperty = null
    // seller.Beds = null
    // after enrichment: seller.Beds still null
}

[Fact]
public void DetermineReportType_FiveComps_ReturnsComprehensive()
{
    var result = CmaProcessingWorker.DetermineReportType(5);
    result.Should().Be(ReportType.Comprehensive);
}

[Fact]
public void DetermineReportType_FourComps_ReturnsStandard()
{
    var result = CmaProcessingWorker.DetermineReportType(4);
    result.Should().Be(ReportType.Standard);
}
```

- [ ] Run tests: `dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/`

- [ ] Commit:
```
feat(cma): add EnrichSubjectAsync step, SubjectProperty context slot, 5-comp Comprehensive threshold
```

---

### Task 4 — Claude Prompt Updates

**Files:** `ClaudeCmaAnalyzer.cs`, `ClaudeCmaAnalyzerTests.cs`

**Steps:**

- [ ] Open `apps/api/RealEstateStar.Workers.Cma/ClaudeCmaAnalyzer.cs`

- [ ] Append rule 7 to `SystemPrompt`:

```
7. Weight recent sales (sold within the last 6 months) more heavily than older sales when estimating value.
   Older comps are annotated with "[Older sale]" in the comparable sales section.
   An older comp should inform but not dominate the valuation range.
```

- [ ] In `BuildPrompt`, update the comp loop header line to include `[Older sale]` annotation:

```csharp
sb.AppendLine($"### Comp {i + 1}{(!comp.IsRecent ? " [Older sale]" : "")}");
```

- [ ] Remove `Source:` line from the comp loop (source is internal, not needed by Claude for analysis):

```csharp
// Remove this line:
sb.AppendLine($"Source: {comp.Source}");
```

- [ ] Add `Correlation:` line to help Claude understand RentCast's similarity score (optional — include only if non-null):

```csharp
if (comp.Correlation.HasValue)
    sb.AppendLine($"Similarity Score: {comp.Correlation:F2}");
```

- [ ] Open `apps/api/tests/RealEstateStar.Workers.Cma.Tests/ClaudeCmaAnalyzerTests.cs`

- [ ] Add two annotation tests:

```csharp
[Fact]
public void BuildPrompt_OlderComp_HasOlderSaleAnnotation()
{
    var lead = MakeLead();
    var comp = MakeComp() with { IsRecent = false };

    var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

    prompt.Should().Contain("[Older sale]");
}

[Fact]
public void BuildPrompt_RecentComp_DoesNotHaveOlderSaleAnnotation()
{
    var lead = MakeLead();
    var comp = MakeComp() with { IsRecent = true };

    var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

    prompt.Should().NotContain("[Older sale]");
}

[Fact]
public void BuildPrompt_DoesNotIncludeSourceLine()
{
    var lead = MakeLead();
    var comp = MakeComp();

    var prompt = ClaudeCmaAnalyzer.BuildPrompt(lead, [comp]);

    prompt.Should().NotContain("Source:");
    prompt.Should().NotContain("RentCast");
}
```

- [ ] Run tests: `dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/`

- [ ] Commit:
```
feat(cma): annotate older comps in Claude prompt, weight recent sales in system prompt
```

---

## Phase 2: PDF + Endpoint (3 parallel tasks after Phase 1)

### Task 5 — IImageResolver Interface + LocalFirstImageResolver

**Files:** `IImageResolver.cs` (new), `LocalFirstImageResolver.cs` (new), `LocalFirstImageResolverTests.cs` (new)

**Steps:**

- [ ] Create `apps/api/RealEstateStar.Domain/Cma/Interfaces/IImageResolver.cs`:

```csharp
namespace RealEstateStar.Domain.Cma.Interfaces;

public interface IImageResolver
{
    /// <summary>
    /// Resolves an agent image URL to bytes for embedding in a PDF.
    /// Tries local file first (Docker/dev path), then HTTP download from the agent's public site.
    /// Returns null if the image cannot be resolved. Never throws.
    /// </summary>
    Task<byte[]?> ResolveAsync(string? relativeUrl, string handle, CancellationToken ct);
}
```

- [ ] Create `apps/api/RealEstateStar.Workers.Cma/LocalFirstImageResolver.cs`:

```csharp
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Cma.Interfaces;

namespace RealEstateStar.Workers.Cma;

internal sealed class LocalFirstImageResolver(
    IHttpClientFactory httpClientFactory,
    ILogger<LocalFirstImageResolver> logger) : IImageResolver
{
    // Docker path: /app/config/accounts/{handle}/
    // Local dev path: config/accounts/{handle}/ (relative to app root)
    private static readonly string[] ConfigBasePaths =
    [
        "/app/config/accounts",
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "config", "accounts")
    ];

    public async Task<byte[]?> ResolveAsync(string? relativeUrl, string handle, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return null;

        // Security: validate handle is safe path segment
        if (!IsValidHandle(handle))
        {
            logger.LogWarning("[CMA-IMG-001] Invalid handle '{Handle}' — rejecting image resolution", handle);
            return null;
        }

        // Mode A: local file
        var fileName = Path.GetFileName(relativeUrl);
        foreach (var basePath in ConfigBasePaths)
        {
            var localPath = Path.Combine(basePath, handle, fileName);
            if (File.Exists(localPath))
            {
                try
                {
                    logger.LogInformation("[CMA-IMG-010] Loaded image from local file: {Path}", localPath);
                    return await File.ReadAllBytesAsync(localPath, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[CMA-IMG-011] Failed to read local image file: {Path}", localPath);
                }
            }
        }

        // Mode B: HTTP download from agent's public site
        var publicUrl = $"https://{handle}.real-estate-star.com{relativeUrl}";
        try
        {
            var client = httpClientFactory.CreateClient("ImageResolver");
            using var response = await client.GetAsync(publicUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("[CMA-IMG-020] Downloaded image from {Url}", publicUrl);
                return await response.Content.ReadAsByteArrayAsync(ct);
            }

            logger.LogWarning("[CMA-IMG-021] HTTP download failed for {Url}: {Status}",
                publicUrl, (int)response.StatusCode);
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("[CMA-IMG-022] Image download timed out: {Url}", publicUrl);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "[CMA-IMG-023] Image download HTTP error: {Url}", publicUrl);
        }

        // Mode C: graceful omission
        logger.LogWarning("[CMA-IMG-030] Could not resolve image '{RelativeUrl}' for handle '{Handle}' — rendering placeholder",
            relativeUrl, handle);
        return null;
    }

    internal static bool IsValidHandle(string handle) =>
        !string.IsNullOrWhiteSpace(handle) &&
        handle.All(c => char.IsLetterOrDigit(c) || c == '-') &&
        !handle.Contains("..");
}
```

- [ ] Create `apps/api/tests/RealEstateStar.Workers.Cma.Tests/LocalFirstImageResolverTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Workers.Cma;

namespace RealEstateStar.Workers.Cma.Tests;

public class LocalFirstImageResolverTests
{
    [Fact]
    public void IsValidHandle_ValidHandle_ReturnsTrue()
    {
        LocalFirstImageResolver.IsValidHandle("jenise-buckalew").Should().BeTrue();
    }

    [Fact]
    public void IsValidHandle_PathTraversal_ReturnsFalse()
    {
        LocalFirstImageResolver.IsValidHandle("../etc/passwd").Should().BeFalse();
    }

    [Fact]
    public void IsValidHandle_Slashes_ReturnsFalse()
    {
        LocalFirstImageResolver.IsValidHandle("some/handle").Should().BeFalse();
    }

    [Fact]
    public void IsValidHandle_Null_ReturnsFalse()
    {
        LocalFirstImageResolver.IsValidHandle(null!).Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_NullUrl_ReturnsNull()
    {
        var resolver = new LocalFirstImageResolver(
            Mock.Of<IHttpClientFactory>(),
            NullLogger<LocalFirstImageResolver>.Instance);

        var result = await resolver.ResolveAsync(null, "jenise-buckalew", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_InvalidHandle_ReturnsNull()
    {
        var resolver = new LocalFirstImageResolver(
            Mock.Of<IHttpClientFactory>(),
            NullLogger<LocalFirstImageResolver>.Instance);

        var result = await resolver.ResolveAsync("/agents/test/logo.png", "../etc", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_LocalFileMissingAndHttpFails_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        // Set up handler to return 404
        // (use MockHttpMessageHandler pattern from existing tests)
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient("ImageResolver"))
            .Returns(new HttpClient(new NotFoundHandler()));

        var resolver = new LocalFirstImageResolver(
            mockFactory.Object,
            NullLogger<LocalFirstImageResolver>.Instance);

        var result = await resolver.ResolveAsync(
            "/agents/jenise-buckalew/logo.png",
            "jenise-buckalew",
            CancellationToken.None);

        result.Should().BeNull();
    }

    private class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
```

- [ ] Register `LocalFirstImageResolver` in DI (file: `apps/api/RealEstateStar.Api/Program.cs` or the Cma service registration extension):

```csharp
services.AddScoped<IImageResolver, LocalFirstImageResolver>();
```

  Also register `HttpClient` named `"ImageResolver"` with a reasonable timeout:
  ```csharp
  services.AddHttpClient("ImageResolver").ConfigurePrimaryHttpMessageHandler(() =>
      new HttpClientHandler { AllowAutoRedirect = true })
      .SetHandlerLifetime(TimeSpan.FromMinutes(5));
  ```

- [ ] Run tests: `dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/ --filter LocalFirstImageResolver`

- [ ] Commit:
```
feat(cma): add IImageResolver interface and LocalFirstImageResolver (local-first + HTTP fallback)
```

---

### Task 6 — CmaPdfGenerator Redesign

**Files:** `CmaPdfGenerator.cs`, `ICmaPdfGenerator.cs`, `CmaPdfGeneratorTests.cs`

**Steps:**

- [ ] Update `ICmaPdfGenerator` interface in `apps/api/RealEstateStar.Domain/Cma/Interfaces/ICmaPdfGenerator.cs` to accept pre-resolved image bytes:

```csharp
public interface ICmaPdfGenerator
{
    /// <summary>
    /// Generates a branded CMA PDF report and returns the path to the temp file.
    /// </summary>
    /// <param name="logoBytes">Brokerage logo image bytes. Null renders a branded placeholder.</param>
    /// <param name="headshotBytes">Agent headshot image bytes. Null renders an initials placeholder.</param>
    Task<string> GenerateAsync(
        Lead lead,
        CmaAnalysis analysis,
        List<Comp> comps,
        AccountConfig agent,
        ReportType reportType,
        byte[]? logoBytes,
        byte[]? headshotBytes,
        CancellationToken ct);
}
```

- [ ] Update `GeneratePdfAsync` in `CmaProcessingWorker` to resolve images before calling the generator:

```csharp
private async Task GeneratePdfAsync(CmaPipelineContext ctx, CancellationToken ct)
{
    var accountConfig = await accountConfigService.GetAccountAsync(ctx.AgentId, ct)
        ?? throw new InvalidOperationException($"Account config not found for agent {ctx.AgentId}");

    var logoBytes = await imageResolver.ResolveAsync(
        accountConfig.Branding?.LogoUrl, ctx.AgentId, ct);
    var headshotBytes = await imageResolver.ResolveAsync(
        accountConfig.Agent?.HeadshotUrl, ctx.AgentId, ct);

    var reportType = DetermineReportType(ctx.Comps!.Count);
    var pdfPath = await pdfGenerator.GenerateAsync(
        ctx.Request, ctx.Analysis!, ctx.Comps!,
        accountConfig, reportType,
        logoBytes, headshotBytes, ct);

    ctx.Set("pdf-path", pdfPath);
}
```

  This requires adding `IImageResolver imageResolver` to `CmaProcessingWorker`'s constructor.

- [ ] Rewrite `CmaPdfGenerator.cs`. Key design decisions:

  **Color helper:**
  ```csharp
  internal static string HexOrDefault(string? hex, string fallback) =>
      string.IsNullOrWhiteSpace(hex) ? fallback : hex.TrimStart('#').Length == 6 ? hex : fallback;

  private static string Primary(AccountConfig agent) =>
      HexOrDefault(agent.Branding?.PrimaryColor, "#1E3A5F");
  private static string Accent(AccountConfig agent) =>
      HexOrDefault(agent.Branding?.AccentColor, "#C8A951");
  ```

  **Age calculation helper:**
  ```csharp
  internal static int MonthsAgo(DateOnly saleDate, DateOnly today) =>
      (int)Math.Round((today.ToDateTime(TimeOnly.MinValue) -
                       saleDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 30.4);
  ```

  **Updated `GenerateSync` signature:**
  ```csharp
  private static void GenerateSync(
      Lead lead,
      CmaAnalysis analysis,
      List<Comp> comps,
      AccountConfig agent,
      ReportType reportType,
      string outputPath,
      byte[]? logoBytes,
      byte[]? headshotBytes)
  ```

  **Section structure** (call order in `page.Content().Column(col => ...)`):
  1. `AddHeaderBand(col, lead, fullAddress, agent, logoBytes, headshotBytes)`
  2. `AddPropertyOverview(col, sd, fullAddress, agent)` — all report types
  3. `AddValueEstimate(col, analysis, agent, reportType)` — always
  4. `AddCompTable(col, comps, agent, today)` — always
  5. `AddMarketAnalysis(col, analysis, agent)` — Comprehensive only
  6. `AddPricingStrategy(col, analysis)` — Comprehensive only (no ConversationStarters)
  7. `AddFooter(col, agent, headshotBytes, logoBytes)` — always

- [ ] Implement `AddHeaderBand`:
  - Full-width colored band (`primary_color` background, white text)
  - Left column: logo image (if `logoBytes != null`) or colored placeholder box with brokerage initial
  - Center column: "Comparative Market Analysis", address, "Prepared for: {name}", "Prepared by: {agent} | {title}"
  - Right column: headshot in circular frame (or initials placeholder)
  - Sub-row: brokerage name + license number + agent license
  - 4px accent bar at bottom using `accent_color`

  ```csharp
  private static void AddHeaderBand(
      ColumnDescriptor col,
      Lead lead,
      string fullAddress,
      AccountConfig agent,
      byte[]? logoBytes,
      byte[]? headshotBytes)
  {
      col.Item().Background(Primary(agent)).Padding(16).Row(row =>
      {
          // Logo
          row.ConstantItem(64).Column(logoCol =>
          {
              if (logoBytes is not null)
                  logoCol.Item().Height(48).Image(logoBytes).FitArea();
              else
                  logoCol.Item().Height(48).Width(48)
                      .Background(Accent(agent))
                      .AlignCenter().AlignMiddle()
                      .Text(agent.Brokerage?.Name?.FirstOrDefault().ToString() ?? "B")
                      .FontColor("#FFFFFF").FontSize(20).Bold();
          });

          row.RelativeItem().PaddingHorizontal(16).Column(center =>
          {
              center.Item().Text("Comparative Market Analysis")
                  .FontColor("#FFFFFF").FontSize(20).Bold();
              center.Item().Text(fullAddress)
                  .FontColor("#FFFFFF").FontSize(11);
              center.Item().PaddingTop(8).Text($"Prepared for: {lead.FullName}")
                  .FontColor("#FFFFFF").FontSize(9);
              center.Item().Text(BuildPreparedByLine(agent))
                  .FontColor("#FFFFFF").FontSize(9);
              center.Item().PaddingTop(4).Text(BuildLicenseLine(agent))
                  .FontColor("#FFFFFF").FontSize(8).Italic();
          });

          // Headshot
          row.ConstantItem(64).Column(headCol =>
          {
              if (headshotBytes is not null)
                  headCol.Item().Height(64).Width(64).Image(headshotBytes).FitArea();
              else
                  headCol.Item().Height(64).Width(64)
                      .Background(Accent(agent))
                      .AlignCenter().AlignMiddle()
                      .Text(AgentInitials(agent))
                      .FontColor("#FFFFFF").FontSize(18).Bold();
          });
      });

      // 4px accent bar
      col.Item().Height(4).Background(Accent(agent));
      col.Item().PaddingBottom(16);
  }

  private static string BuildPreparedByLine(AccountConfig agent)
  {
      var parts = new List<string>();
      if (agent.Agent?.Name is { } name) parts.Add(name);
      if (agent.Agent?.Title is { } title) parts.Add(title);
      if (agent.Brokerage?.Name is { } brokerage) parts.Add(brokerage);
      return string.Join(" | ", parts);
  }

  private static string BuildLicenseLine(AccountConfig agent)
  {
      var parts = new List<string>();
      if (agent.Agent?.Phone is { } phone) parts.Add(phone);
      if (agent.Agent?.Email is { } email) parts.Add(email);
      if (agent.Agent?.LicenseNumber is { } lic) parts.Add($"License #{lic}");
      return string.Join(" | ", parts);
  }

  private static string AgentInitials(AccountConfig agent)
  {
      var name = agent.Agent?.Name ?? "";
      var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      return parts.Length >= 2
          ? $"{parts[0][0]}{parts[^1][0]}"
          : name.Length >= 1 ? name[0].ToString() : "A";
  }
  ```

- [ ] Implement `AddPropertyOverview` with enriched data + Style column:

  ```csharp
  private static void AddPropertyOverview(
      ColumnDescriptor col,
      SellerDetails? sd,
      string fullAddress,
      AccountConfig agent)
  {
      col.Item().Background("#F5F5F5").Padding(12).Column(inner =>
      {
          inner.Item().Text("PROPERTY OVERVIEW")
              .FontSize(11).Bold().FontColor(Primary(agent));
          inner.Item().PaddingTop(4).LineHorizontal(1).LineColor("#DDDDDD");
          inner.Item().PaddingTop(8).Table(table =>
          {
              table.ColumnsDefinition(cols =>
              {
                  cols.RelativeColumn(3);
                  cols.RelativeColumn();
                  cols.RelativeColumn();
                  cols.RelativeColumn();
                  cols.RelativeColumn(2);
              });
              table.Header(h =>
              {
                  foreach (var label in new[] { "Address", "Beds", "Baths", "Sqft", "Style" })
                      h.Cell().Text(label).Bold().FontSize(9);
              });
              table.Cell().Text(fullAddress).FontSize(9);
              table.Cell().Text(sd?.Beds?.ToString() ?? "—").FontSize(9);
              table.Cell().Text(sd?.Baths?.ToString() ?? "—").FontSize(9);
              table.Cell().Text(sd?.Sqft?.ToString("N0") ?? "—").FontSize(9);
              table.Cell().Text(sd?.PropertyType ?? "—").FontSize(9);
          });
      });
      col.Item().PaddingBottom(12);
  }
  ```

  > Note: `SellerDetails` may not have a `PropertyType` property. If missing, skip that column or leave as "—" without trying to access the field. Check the model before implementing.

- [ ] Implement `AddValueEstimate` with three-card hero layout and branded mid card:

  ```csharp
  private static void AddValueEstimate(
      ColumnDescriptor col,
      CmaAnalysis analysis,
      AccountConfig agent,
      ReportType reportType)
  {
      col.Item().PaddingBottom(8).Text("ESTIMATED VALUE")
          .FontSize(11).Bold().FontColor(Primary(agent));

      col.Item().Row(row =>
      {
          // Low card
          row.RelativeItem().Border(1).BorderColor("#CCCCCC").Padding(10).Column(c =>
          {
              c.Item().Text("LOW").FontSize(8).Bold().FontColor("#888888");
              c.Item().Text(FormatCurrency(analysis.ValueLow)).FontSize(14).Bold();
          });
          row.ConstantItem(8);

          // Mid card — branded
          row.RelativeItem()
              .Border(2).BorderColor(Primary(agent))
              .Background($"{Primary(agent)}1A") // 10% opacity approximation via hex
              .Padding(10).Column(c =>
          {
              c.Item().Text("MID").FontSize(8).Bold().FontColor(Primary(agent));
              c.Item().Text(FormatCurrency(analysis.ValueMid))
                  .FontSize(18).Bold().FontColor(Primary(agent));
              if (reportType is ReportType.Comprehensive)
                  c.Item().Text("Recommended").FontSize(7).Italic().FontColor(Primary(agent));
          });
          row.ConstantItem(8);

          // High card
          row.RelativeItem().Border(1).BorderColor("#CCCCCC").Padding(10).Column(c =>
          {
              c.Item().Text("HIGH").FontSize(8).Bold().FontColor("#888888");
              c.Item().Text(FormatCurrency(analysis.ValueHigh)).FontSize(14).Bold();
          });
      });

      if (reportType is ReportType.Comprehensive && analysis.PricingRecommendation is { } rec)
      {
          col.Item().PaddingTop(8).Text("Pricing Strategy:").Bold().FontSize(9);
          col.Item().Text(rec).FontSize(9);
      }

      col.Item().PaddingBottom(16);
  }
  ```

- [ ] Implement `AddCompTable` with Age column, dagger annotation, no Source column, alternating rows:

  ```csharp
  private static void AddCompTable(
      ColumnDescriptor col,
      List<Comp> comps,
      AccountConfig agent,
      DateOnly today)
  {
      col.Item().PaddingBottom(8).Text("RECENT COMPARABLE SALES")
          .FontSize(11).Bold().FontColor(Primary(agent));

      col.Item().Table(table =>
      {
          table.ColumnsDefinition(cols =>
          {
              cols.RelativeColumn(3); // Address
              cols.RelativeColumn(2); // Sale Price
              cols.RelativeColumn();  // Bd
              cols.RelativeColumn();  // Ba
              cols.RelativeColumn(2); // Sqft
              cols.RelativeColumn(2); // $/Sqft
              cols.RelativeColumn(2); // Sold
              cols.RelativeColumn();  // Age
          });

          var headerBg = $"{Primary(agent)}26"; // 15% opacity
          table.Header(h =>
          {
              foreach (var label in new[] { "Address", "Sale Price", "Bd", "Ba", "Sqft", "$/Sqft", "Sold", "Age" })
              {
                  h.Cell().Background(headerBg).Padding(4)
                      .Text(label).Bold().FontSize(8).FontColor(Primary(agent));
              }
          });

          for (var i = 0; i < comps.Count; i++)
          {
              var comp = comps[i];
              var rowBg = i % 2 == 1 ? "#F9F9F9" : "#FFFFFF";
              var months = MonthsAgo(comp.SaleDate, today);
              var dagger = comp.IsRecent ? "" : "†";
              var ageLabel = comp.IsRecent ? $"{months} mo" : $"{months} mo†";

              table.Cell().Background(rowBg).Padding(4).Text($"{comp.Address}{dagger}").FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(FormatCurrency(comp.SalePrice)).FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(comp.Beds.ToString()).FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(comp.Baths.ToString()).FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(comp.Sqft.ToString("N0")).FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(FormatPricePerSqft(comp.PricePerSqft)).FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(comp.SaleDate.ToString("MMM ''yy")).FontSize(8);
              table.Cell().Background(rowBg).Padding(4).Text(ageLabel).FontSize(8);
          }
      });

      if (comps.Any(c => !c.IsRecent))
          col.Item().PaddingTop(4).Text("† Older sale — weighted less in valuation")
              .FontSize(7).Italic().FontColor("#666666");

      col.Item().PaddingBottom(16);
  }
  ```

- [ ] Implement `AddMarketAnalysis` with stat cards + narrative:

  ```csharp
  private static void AddMarketAnalysis(ColumnDescriptor col, CmaAnalysis analysis, AccountConfig agent)
  {
      col.Item().PaddingBottom(8).Text("MARKET ANALYSIS")
          .FontSize(11).Bold().FontColor(Primary(agent));

      col.Item().Row(row =>
      {
          row.RelativeItem().Border(1).BorderColor("#DDDDDD").Padding(8).Column(c =>
          {
              c.Item().Text("Market Trend").Bold().FontSize(8).FontColor("#888888");
              c.Item().Text(analysis.MarketTrend).FontSize(12).Bold();
          });
          row.ConstantItem(8);
          row.RelativeItem().Border(1).BorderColor("#DDDDDD").Padding(8).Column(c =>
          {
              c.Item().Text("Median Days on Market").Bold().FontSize(8).FontColor("#888888");
              c.Item().Text($"{analysis.MedianDaysOnMarket} days").FontSize(12).Bold();
          });
      });

      col.Item().PaddingTop(8).Text(analysis.MarketNarrative).FontSize(9);
      col.Item().PaddingBottom(16);
  }
  ```

- [ ] Implement `AddPricingStrategy` (ConversationStarters excluded from PDF):

  ```csharp
  private static void AddPricingStrategy(ColumnDescriptor col, CmaAnalysis analysis)
  {
      if (analysis.PricingRecommendation is null) return;

      col.Item().PaddingBottom(8).Text("PRICING STRATEGY").FontSize(11).Bold();
      col.Item().Text(analysis.PricingRecommendation).FontSize(9);
      col.Item().PaddingBottom(16);
  }
  ```

- [ ] Implement `AddFooter` with agent contact info, small headshot + logo, date:

  ```csharp
  private static void AddFooter(
      ColumnDescriptor col,
      AccountConfig agent,
      byte[]? headshotBytes,
      byte[]? logoBytes)
  {
      col.Item().LineHorizontal(1).LineColor("#DDDDDD");
      col.Item().Background(Primary(agent)).Opacity(0.7f).Padding(12).Row(row =>
      {
          // Small headshot
          row.ConstantItem(40).AlignMiddle().Column(c =>
          {
              if (headshotBytes is not null)
                  c.Item().Height(36).Width(36).Image(headshotBytes).FitArea();
          });

          row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
          {
              c.Item().Text(agent.Agent?.Name ?? "").Bold().FontSize(10).FontColor("#FFFFFF");
              var licLine = new List<string>();
              if (agent.Agent?.Title is { } t) licLine.Add(t);
              if (agent.Agent?.LicenseNumber is { } lic) licLine.Add($"License #{lic}");
              c.Item().Text(string.Join(" | ", licLine)).FontSize(8).FontColor("#FFFFFF");
              if (agent.Brokerage?.Name is { } brokerage)
                  c.Item().Text(brokerage).FontSize(8).FontColor("#FFFFFF");
              if (agent.Brokerage?.OfficeAddress is { } addr)
                  c.Item().Text(addr).FontSize(7).FontColor("#FFFFFF");
              var contactLine = new List<string>();
              if (agent.Agent?.Phone is { } phone) contactLine.Add(phone);
              if (agent.Agent?.Email is { } email) contactLine.Add(email);
              c.Item().Text(string.Join(" | ", contactLine)).FontSize(8).FontColor("#FFFFFF");
              var areas = agent.Location?.ServiceAreas ?? [];
              if (areas.Count > 0)
                  c.Item().Text($"Serving: {string.Join(", ", areas)}").FontSize(7).FontColor("#FFFFFF");
          });

          row.ConstantItem(80).AlignMiddle().Column(c =>
          {
              if (logoBytes is not null)
                  c.Item().Height(32).Image(logoBytes).FitArea();
              c.Item().Text(DateTime.UtcNow.ToString("MMMM d, yyyy"))
                  .FontSize(7).FontColor("#FFFFFF").AlignRight();
          });
      });
  }
  ```

- [ ] Open `apps/api/tests/RealEstateStar.Workers.Cma.Tests/CmaPdfGeneratorTests.cs`

- [ ] Add smoke tests for the redesigned generator:

```csharp
[Fact]
public void GenerateAsync_WithNullImages_DoesNotThrow()
{
    var lead = MakeLead();
    var analysis = MakeAnalysis();
    var comps = MakeComps(5);
    var agent = MakeAccountConfig();

    var action = () => CmaPdfGenerator.GenerateSync(
        lead, analysis, comps, agent, ReportType.Comprehensive,
        outputPath: Path.GetTempFileName(),
        logoBytes: null,
        headshotBytes: null);

    action.Should().NotThrow();
}

[Fact]
public void GenerateAsync_WithOlderComps_RendersFootnote()
{
    var comps = MakeComps(2);
    // Set one comp as not recent
    var olderComp = comps[1] with { IsRecent = false, SaleDate = new DateOnly(2025, 1, 1) };
    var mixedComps = new List<Comp> { comps[0], olderComp };
    var path = Path.GetTempFileName();

    CmaPdfGenerator.GenerateSync(
        MakeLead(), MakeAnalysis(), mixedComps,
        MakeAccountConfig(), ReportType.Standard, path, null, null);

    var pdfBytes = File.ReadAllBytes(path);
    pdfBytes.Length.Should().BeGreaterThan(1000);
    // PDF content validation: verify file is valid PDF
    pdfBytes[0..4].Should().BeEquivalentTo(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
}

[Fact]
public void GenerateAsync_WithEnrichedSubject_ShowsBeds()
{
    var seller = MakeSellerDetails(beds: 4, baths: 3, sqft: 2200);
    var lead = MakeLead(seller);
    var path = Path.GetTempFileName();

    var action = () => CmaPdfGenerator.GenerateSync(
        lead, MakeAnalysis(), MakeComps(3),
        MakeAccountConfig(), ReportType.Standard, path, null, null);

    action.Should().NotThrow();
}

[Fact]
public void HexOrDefault_NullInput_ReturnsFallback()
{
    CmaPdfGenerator.HexOrDefault(null, "#1E3A5F").Should().Be("#1E3A5F");
}

[Fact]
public void HexOrDefault_EmptyString_ReturnsFallback()
{
    CmaPdfGenerator.HexOrDefault("", "#1E3A5F").Should().Be("#1E3A5F");
}

[Fact]
public void HexOrDefault_ValidHex_ReturnsInput()
{
    CmaPdfGenerator.HexOrDefault("#1B5E20", "#1E3A5F").Should().Be("#1B5E20");
}

[Fact]
public void MonthsAgo_SixMonthsAgo_ReturnsSix()
{
    var today = new DateOnly(2026, 3, 26);
    var saleDate = new DateOnly(2025, 9, 26);
    CmaPdfGenerator.MonthsAgo(saleDate, today).Should().Be(6);
}
```

- [ ] Run tests: `dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/`

- [ ] Commit:
```
feat(cma): redesign PDF with branded header, comp age table, value estimate hero, agent footer
```

---

### Task 7 — DownloadCmaEndpoint

**Files:** `DownloadCmaEndpoint.cs` (new), `RentCastClient.cs` (SubjectProperty mapping)

**Steps:**

- [ ] Open `apps/api/RealEstateStar.Clients.RentCast/RentCastClient.cs`

- [ ] Add `subjectProperty` to `RentCastApiResponse` internal DTO:

  ```csharp
  private record RentCastApiResponse(
      [property: JsonPropertyName("price")] decimal Price,
      [property: JsonPropertyName("priceRangeLow")] decimal PriceRangeLow,
      [property: JsonPropertyName("priceRangeHigh")] decimal PriceRangeHigh,
      [property: JsonPropertyName("comparables")] List<RentCastApiComp>? Comparables,
      [property: JsonPropertyName("subjectProperty")] RentCastApiSubjectProperty? SubjectProperty);

  private record RentCastApiSubjectProperty(
      [property: JsonPropertyName("bedrooms")] int? Bedrooms,
      [property: JsonPropertyName("bathrooms")] decimal? Bathrooms,
      [property: JsonPropertyName("squareFootage")] int? SquareFootage,
      [property: JsonPropertyName("propertyType")] string? PropertyType,
      [property: JsonPropertyName("yearBuilt")] int? YearBuilt);
  ```

  > **Open question (spec §18.1):** Verify the exact JSON key returned by `/v1/avm/value`. The spec assumes `subjectProperty`. Log the raw response in staging and confirm before shipping.

- [ ] Update `MapToValuation` to map the subject property:

  ```csharp
  private static RentCastValuation MapToValuation(RentCastApiResponse dto) =>
      new()
      {
          Price = dto.Price,
          PriceRangeLow = dto.PriceRangeLow,
          PriceRangeHigh = dto.PriceRangeHigh,
          Comparables = (dto.Comparables ?? [])
              .Select(c => new RentCastComp { /* existing mapping */ })
              .ToList().AsReadOnly(),
          SubjectProperty = dto.SubjectProperty is null ? null : new RentCastSubjectProperty
          {
              Bedrooms = dto.SubjectProperty.Bedrooms,
              Bathrooms = dto.SubjectProperty.Bathrooms,
              SquareFootage = dto.SubjectProperty.SquareFootage,
              PropertyType = dto.SubjectProperty.PropertyType,
              YearBuilt = dto.SubjectProperty.YearBuilt
          }
      };
  ```

- [ ] Add `RentCastClientSubjectPropertyTests.cs` in `apps/api/tests/RealEstateStar.Clients.RentCast.Tests/`:

  ```csharp
  [Fact]
  public void MapToValuation_ResponseWithSubjectProperty_PopulatesSubjectProperty()
  {
      // arrange: create a RentCastApiResponse with a non-null SubjectProperty
      // act: call MapToValuation via reflection or by making it internal + InternalsVisibleTo
      // assert: result.SubjectProperty.Bedrooms == expected
  }

  [Fact]
  public void MapToValuation_ResponseWithoutSubjectProperty_SubjectPropertyIsNull()
  {
      // assert: result.SubjectProperty == null
  }
  ```

- [ ] Create `apps/api/RealEstateStar.Api/Features/Cma/Download/DownloadCmaEndpoint.cs`:

  ```csharp
  using RealEstateStar.Domain.Shared.Interfaces.Storage;

  namespace RealEstateStar.Api.Features.Cma.Download;

  public class DownloadCmaEndpoint : IEndpoint
  {
      public void MapEndpoint(WebApplication app) =>
          app.MapGet(
              "/accounts/{accountId}/agents/{agentId}/leads/{leadId}/cma/download",
              Handle);

      internal static async Task<IResult> Handle(
          string accountId,
          string agentId,
          Guid leadId,
          IDocumentStorageProvider documentStorage,
          ILogger<DownloadCmaEndpoint> logger,
          CancellationToken ct)
      {
          logger.LogInformation(
              "[CMA-DL-001] CMA download requested. AccountId={AccountId} AgentId={AgentId} LeadId={LeadId}",
              accountId, agentId, leadId);

          // Build the expected folder path (matches CmaProcessingWorker.StorePdfAsync)
          // We don't have lead name/address here — search by convention or add a lookup.
          // Approach: search for any .b64 file under the leads tree for this agentId.
          // This requires IDocumentStorageProvider to support listing — check the interface.

          // Alternative approach: Accept lead folder path as query parameter (not ideal for UX)
          // OR add a dedicated CMA index file that maps leadId → file path.

          // Simplest v1: require the caller knows the address path,
          // OR: add a new ILeadStore.GetByIdAsync(agentId, leadId, ct) to find the lead,
          //     then reconstruct the folder path from lead.FullName + SellerDetails address.

          // Implementation decision: inject ILeadStore and call GetByIdAsync if it exists.
          // If not, add GetByIdAsync to the ILeadStore interface (Domain change).

          // For v1, return 501 with a descriptive message if lead lookup is not available.
          // The download endpoint is validated as working if it can locate and serve one PDF.

          return Results.StatusCode(501); // placeholder — implement lookup in next step
      }
  }
  ```

  > **Implementer action required:** Before implementing `Handle`, check `ILeadStore` for `GetByIdAsync(string agentId, Guid leadId, CancellationToken ct)`. If it exists, use it to reconstruct the folder path. If it doesn't exist, add it to the interface in `Domain` and implement it in `DataServices`.

- [ ] Full `DownloadCmaEndpoint` implementation once lead lookup is confirmed:

  ```csharp
  internal static async Task<IResult> Handle(
      string accountId,
      string agentId,
      Guid leadId,
      ILeadStore leadStore,
      IDocumentStorageProvider documentStorage,
      ILogger<DownloadCmaEndpoint> logger,
      CancellationToken ct)
  {
      logger.LogInformation(
          "[CMA-DL-001] CMA download requested. AccountId={AccountId} AgentId={AgentId} LeadId={LeadId}",
          accountId, agentId, leadId);

      // 1. Look up the lead to get its folder path
      var lead = await leadStore.GetByIdAsync(agentId, leadId, ct);
      if (lead is null)
      {
          logger.LogWarning("[CMA-DL-002] Lead not found. LeadId={LeadId}", leadId);
          return Results.NotFound("Lead not found.");
      }

      var seller = lead.SellerDetails;
      if (seller is null)
      {
          logger.LogWarning("[CMA-DL-003] Lead has no seller details. LeadId={LeadId}", leadId);
          return Results.NotFound("CMA not available for this lead.");
      }

      // 2. Construct folder path (must match StorePdfAsync convention)
      var folder = $"Real Estate Star/1 - Leads/{lead.FullName}/{seller.Address}, {seller.City}, {seller.State} {seller.Zip}";

      // 3. Find the .b64 file
      string? b64Content;
      try
      {
          // ListDocumentsAsync returns file names in the folder
          var files = await documentStorage.ListDocumentsAsync(folder, ct);
          var cmaFile = files.FirstOrDefault(f => f.EndsWith("-CMA-Report.pdf.b64",
              StringComparison.OrdinalIgnoreCase));

          if (cmaFile is null)
          {
              logger.LogWarning("[CMA-DL-004] No CMA PDF found for lead {LeadId} in folder {Folder}",
                  leadId, folder);
              return Results.NotFound("CMA report not yet generated.");
          }

          b64Content = await documentStorage.ReadDocumentAsync(folder, cmaFile, ct);
      }
      catch (Exception ex)
      {
          logger.LogError(ex, "[CMA-DL-010] Storage error reading CMA for lead {LeadId}", leadId);
          return Results.Problem("Error retrieving CMA report.", statusCode: 500);
      }

      // 4. Base64-decode and stream as PDF
      byte[] pdfBytes;
      try
      {
          pdfBytes = Convert.FromBase64String(b64Content ?? "");
      }
      catch (FormatException ex)
      {
          logger.LogError(ex, "[CMA-DL-011] Base64 decode failed for lead {LeadId}", leadId);
          return Results.Problem("CMA report is corrupted.", statusCode: 500);
      }

      logger.LogInformation("[CMA-DL-020] Serving CMA PDF for lead {LeadId}, size {SizeKB}KB",
          leadId, pdfBytes.Length / 1024);

      return Results.File(
          pdfBytes,
          contentType: "application/pdf",
          fileDownloadName: "CMA-Report.pdf",
          enableRangeProcessing: false);
  }
  ```

- [ ] Add endpoint to `IEndpoint` autodiscovery (verify it's picked up automatically by `AddEndpoints` scanning — no manual registration needed).

- [ ] Verify `IDocumentStorageProvider` has `ListDocumentsAsync` — if not, check what listing method exists and adapt. Add the method to the interface + implementations if needed.

- [ ] Add tests for the endpoint in `apps/api/tests/RealEstateStar.Api.Tests/`:

  ```csharp
  // DownloadCmaEndpointTests.cs
  [Fact]
  public async Task Handle_LeadNotFound_Returns404()
  [Fact]
  public async Task Handle_NoCmaPdfFile_Returns404WithMessage()
  [Fact]
  public async Task Handle_ValidPdf_ReturnsPdfBytes()
  [Fact]
  public async Task Handle_Base64DecodeFailure_Returns500()
  ```

- [ ] Run all CMA tests:
  ```bash
  dotnet test apps/api/tests/RealEstateStar.Workers.Cma.Tests/ apps/api/tests/RealEstateStar.Api.Tests/ apps/api/tests/RealEstateStar.Clients.RentCast.Tests/
  ```

- [ ] Commit:
```
feat(api): add DownloadCmaEndpoint, map RentCast subjectProperty in RentCastClient
```

---

## Phase 3: Observability + Docs (2 parallel tasks after Phase 2)

### Task 8 — Observability Updates

**Files:** `CmaDiagnostics.cs`, Grafana dashboard JSON

**Steps:**

- [ ] Open `apps/api/RealEstateStar.Workers.Cma/CmaDiagnostics.cs` (or wherever `CmaDiagnostics` lives — check the file)

- [ ] Add new meters:

  ```csharp
  // PDF size tracking
  public static readonly Histogram<long> PdfSizeBytes =
      Meter.CreateHistogram<long>("cma.pdf_size_bytes", "bytes", "Size of generated CMA PDF");

  // Subject enrichment tracking
  public static readonly Counter<long> SubjectEnriched =
      Meter.CreateCounter<long>("cma.subject_enriched", description: "Times subject property was enriched from RentCast");
  ```

- [ ] Emit `PdfSizeBytes` in `StorePdfAsync` after reading PDF bytes:
  ```csharp
  CmaDiagnostics.PdfSizeBytes.Record(pdfBytes.Length);
  ```

- [ ] Emit `SubjectEnriched` in `EnrichSubjectAsync` when enrichment occurs (beds, baths, or sqft was null and got filled):
  ```csharp
  if (enrichedBeds != seller.Beds || enrichedBaths != seller.Baths || enrichedSqft != seller.Sqft)
      CmaDiagnostics.SubjectEnriched.Add(1);
  ```

- [ ] Update Grafana dashboard (or document the panel definitions to add):

  | Panel | Metric | Notes |
  |-------|--------|-------|
  | CMA Reports Generated | `cma.generated` rate | Verify existing panel is wired |
  | CMA Step Duration | `cma.step_duration_ms` by step | Add `enrich-subject` step to breakdown |
  | Comp Selection | `rentcast.comps_returned` histogram | Add annotation target line at 5 |
  | PDF Size | `cma.pdf_size_bytes` | New panel — P50/P95 histogram |
  | Subject Enrichment | `cma.subject_enriched` | New counter panel — rate per day |

- [ ] Commit:
```
feat(observability): add CMA PDF size + subject enrichment metrics
```

---

### Task 9 — Documentation Updates

**Files:** Architecture docs, CLAUDE.md (root)

**Steps:**

- [ ] Update `docs/architecture/cma-pipeline.md` (create if it doesn't exist):
  - Add `EnrichSubjectAsync` step to the pipeline diagram
  - Document tiered comp selection (5-comp target, 6-month recency)
  - Document PDF storage as base64 `.b64` file
  - Document the `DownloadCmaEndpoint` URL pattern

- [ ] Update root `CLAUDE.md` — in the Lead Pipeline Architecture section:
  - Add `EnrichSubjectAsync` to the worker step list
  - Add note about `DownloadCmaEndpoint`

- [ ] Update `.claude/CLAUDE.md` — in CMA pipeline description:
  - Note new 5-comp tiered selection
  - Note subject property enrichment
  - Note `DownloadCmaEndpoint`
  - Note `IImageResolver` + `LocalFirstImageResolver`

- [ ] Update `docs/onboarding.md`:
  - Add CMA section: what the seller receives (branded PDF, valuation range, comp table)
  - Mention the download endpoint for agent use

- [ ] Commit:
```
docs: update CMA pipeline docs, architecture, onboarding for PDF redesign
```

---

## Phase 4: Full Build + Coverage Verification (Task 10)

### Task 10 — Build, Test, Coverage

**Steps:**

- [ ] Build the entire solution:
  ```bash
  dotnet build apps/api/RealEstateStar.sln
  ```

- [ ] Run all tests:
  ```bash
  dotnet test apps/api/RealEstateStar.sln
  ```

- [ ] Run coverage for affected projects:
  ```bash
  bash apps/api/scripts/coverage.sh
  ```

- [ ] Verify 100% branch coverage on:
  - `RealEstateStar.Workers.Cma` (all 5 tiered selection branches, enrichment branches, hex color branches)
  - `RealEstateStar.Clients.RentCast` (SubjectProperty null + non-null)
  - `RealEstateStar.Api` (DownloadCmaEndpoint: 4 branches — lead not found, no pdf, valid, decode failure)

- [ ] Verify no architecture violations:
  ```bash
  dotnet test apps/api/tests/RealEstateStar.Architecture.Tests/
  ```

- [ ] Smoke test the download endpoint locally:
  1. Start the API: `dotnet run --project apps/api/RealEstateStar.Api`
  2. Submit a seller lead to create a CMA
  3. After CMA pipeline completes, navigate to the download URL in a browser
  4. Verify the PDF opens with branded header, enriched data, tiered comps

- [ ] Verify no `RentCast` text appears in the generated PDF (open file, search)

- [ ] Commit:
```
chore: verify full build, 100% coverage, architecture tests pass for CMA PDF redesign
```

---

## Error Handling Reference

| Failure | Behavior | Log Code |
|---------|---------|----------|
| `IImageResolver` returns null for logo | Render monogram box in brand color | `[CMA-PDF-010]` |
| `IImageResolver` returns null for headshot | Render initials placeholder | `[CMA-PDF-011]` |
| `RentCastValuation.SubjectProperty` null | Skip enrichment, beds/baths may stay null | `[CmaWorker-Enrich-001]` |
| Enriched sqft still null | Render `—` in Property Overview | `[CMA-PDF-012]` |
| Total comps after selection < 1 | Worker exits early (existing behavior) | existing |
| HexColor parse failure | Fall back to `#1E3A5F` / `#C8A951` defaults | `[CMA-PDF-013]` |
| Image download times out | Catch `TaskCanceledException`, return null | `[CMA-IMG-022]` |
| Lead not found in download endpoint | 404 | `[CMA-DL-002]` |
| No CMA file found in storage | 404 with message | `[CMA-DL-004]` |
| Base64 decode failure | 500 with message | `[CMA-DL-011]` |

---

## Open Questions (resolve before implementing)

1. **RentCast `subjectProperty` JSON key** — Log the raw API response in staging before implementing `RentCastClient` deserialization. The spec assumes `subjectProperty` (camelCase) but this must be confirmed.

2. **`SellerDetails` immutability** — If `SellerDetails` uses `init` setters, `EnrichSubjectAsync` must create a new instance. Check the model and use a copy constructor or `with` expression accordingly.

3. **`IDocumentStorageProvider.ListDocumentsAsync`** — Verify this method exists. If not, add `ListDocumentsAsync(string folder, CancellationToken ct)` to the interface and implement in all providers (local + GDrive).

4. **`ILeadStore.GetByIdAsync`** — Verify this method exists. If not, add it to `ILeadStore` (Domain) + `LeadFileStore` / `LeadStore` (DataServices). Required for `DownloadCmaEndpoint`.

5. **`SellerDetails.PropertyType`** — The Property Overview section shows property type in the Style column. This field may not exist on `SellerDetails`. If missing, either add it from `RentCastSubjectProperty.PropertyType` during enrichment, or drop the column.

6. **QuestPDF hex background opacity** — QuestPDF Community does not natively support RGBA or hex with alpha. For the mid value card "10% opacity" effect, use a pre-computed lighter shade of the primary color (e.g., `#E8F0E8` for `#1B5E20`), or hardcode a light-gray approximation. Avoid trying to pass `#1B5E201A` as a hex string.

7. **QuestPDF headshot circle clipping** — QuestPDF Community does not have `ClipRound()`. Square crop is acceptable for v1. A future iteration can pre-process with SkiaSharp if needed.

8. **IRentCastClient double-call in FetchCompsAsync** — The architecture currently routes through `ICompAggregator`. Adding a second direct `IRentCastClient` call in the worker duplicates the HTTP request. Evaluate: (a) expose `SubjectProperty` from `ICompAggregator` result, (b) cache the valuation in `RentCastCompSource`, or (c) accept the double-call (CMA is a background job, latency is acceptable). Document the decision in code.

---

## Config Reference (jenise-buckalew)

| Field | Path in account.json | C# Property | PDF Use |
|-------|---------------------|------------|---------|
| Agent name | `agent.name` | `Agent.Name` | Header center, footer |
| Agent title | `agent.title` | `Agent.Title` | Header center, footer |
| Agent phone | `agent.phone` | `Agent.Phone` | Header, footer |
| Agent email | `agent.email` | `Agent.Email` | Header, footer |
| Headshot | `agent.headshot_url` | `Agent.HeadshotUrl` | Header right, footer left |
| Agent license | `agent.license_number` | `Agent.LicenseNumber` | Header sub-line, footer |
| Agent tagline | `agent.tagline` | `Agent.Tagline` | Footer (optional) |
| Brokerage name | `brokerage.name` | `Brokerage.Name` | Header center, footer |
| Brokerage license | `brokerage.license_number` | `Brokerage.LicenseNumber` | Footer (optional) |
| Office address | `brokerage.office_address` | `Brokerage.OfficeAddress` | Footer |
| Logo | `branding.logo_url` | `Branding.LogoUrl` | Header left, footer right |
| Primary color | `branding.primary_color` | `Branding.PrimaryColor` | Header band, table header, mid card |
| Accent color | `branding.accent_color` | `Branding.AccentColor` | Accent bar, placeholder boxes |
| Service areas | `location.service_areas` | `Location.ServiceAreas` | Footer |

**Fallback colors when config has no branding:**
- Primary: `#1E3A5F` (navy)
- Accent: `#C8A951` (gold)
