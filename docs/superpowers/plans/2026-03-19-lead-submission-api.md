# Lead Submission API — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire up the agent site LeadForm to submit leads to the .NET API, with enrichment, scoring, notification, CMA trigger, buyer home search, GDPR/CCPA deletion, marketing consent audit, and Google Drive change monitoring.

**Architecture:** Server-side submission (browser → Next.js server action on CF Worker → HMAC-signed → .NET API). All lead data stored as human-readable markdown with YAML frontmatter in Google Drive. Fire-and-forget background pipeline: enrich → score → notify → CMA/home search. Pluggable abstractions at every layer (storage, enrichment, notification, search).

**Tech Stack:** .NET 10, Next.js 16, Cloudflare Workers (OpenNext), Google Drive (gws CLI), Claude API, ScraperAPI, Polly (retry/circuit breaker), Cloudflare Turnstile, HMAC-SHA256, OpenTelemetry, Grafana Faro, YamlDotNet

**Spec:** `docs/superpowers/specs/2026-03-19-lead-submission-api-design.md`

---

## Parallel Execution Map

Tasks are grouped into **work streams** that can execute in parallel. Dependencies are explicitly marked.

```
Stream A: Storage Abstractions          Stream B: Domain Models + Mappers     Stream C: Frontend (Agent Site)
  Task 1: IFileStorageProvider            Task 4: Lead domain models             Task 10: Turnstile + honeypot
  Task 2: GDriveStorageProvider           Task 5: LeadMarkdownRenderer           Task 11: Server action + HMAC signing
  Task 3: LocalStorageProvider            Task 6: YamlFrontmatterParser          Task 12: Privacy pages (opt-out, delete, subscribe)
                                          Task 7: LeadMappers                    Task 13: Grafana Faro tracing
         ↓ depends on A+B                        ↓ depends on B
Stream D: Feature Services               Stream E: Security Middleware
  Task 8: ILeadStore + impls               Task 14: ApiKeyHmacMiddleware
  Task 9: IMarketingConsentLog             Task 15: Rate limiting policies
  Task 9b: IDeletionAuditLog
  Task 16: ILeadNotifier (multi-channel)
  Task 17: ILeadEnricher + ScraperLeadEnricher
  Task 18: IHomeSearchProvider + impls
  Task 19: ILeadDataDeletion + impls

         ↓ depends on D+E
Stream F: Endpoints                      Stream G: Observability
  Task 20: SubmitLeadEndpoint              Task 24: LeadDiagnostics (ActivitySource + Meters)
  Task 21: OptOutEndpoint                  Task 25: LlmCostEstimator
  Task 22: SubscribeEndpoint               Task 26: Health checks (Drive, ScraperAPI, Turnstile)
  Task 23: DeleteDataEndpoint              Task 27: Polly retry + circuit breaker policies
  Task 28: RetryFailedLeadsEndpoint
  Task 29: RequestDeletionEndpoint

         ↓ depends on F
Stream H: Drive Change Monitor           Stream I: Infrastructure
  Task 30: DriveChangeMonitor service      Task 32: Promote IGwsService to shared
  Task 31: PollDriveChangesEndpoint        Task 33: CI/CD pipeline updates
                                           Task 34: OpenAPI + TypeScript client codegen

Stream J: Legal & Compliance Review
  Task 35: ADA, CCPA, GDPR, TCA audit

Stream K: Platform Status Page (independent — can run anytime)
  Task 38: Status page on platform.real-estate-star.com

Stream L: Dead Code + Documentation (depends on all implementation)
  Task 39: Dead code cleanup
  Task 40: Documentation updates
```

---

## Pre-Flight: Promote Shared Services

### Task 0: Promote IGwsService to top-level shared service

`IGwsService` currently lives inside `Features/Cma/Services/Gws/`. The Leads feature also depends on it, so it must move to a shared location per vertical slice rules.

**Files:**
- Move: `apps/api/RealEstateStar.Api/Features/Cma/Services/Gws/IGwsService.cs` → `apps/api/RealEstateStar.Api/Services/Gws/IGwsService.cs`
- Move: `apps/api/RealEstateStar.Api/Features/Cma/Services/Gws/GwsService.cs` → `apps/api/RealEstateStar.Api/Services/Gws/GwsService.cs`
- Move: `apps/api/RealEstateStar.Api/Features/Cma/Services/Gws/LeadBriefData.cs` → `apps/api/RealEstateStar.Api/Services/Gws/LeadBriefData.cs`
- Modify: All files in `Features/Cma/` that reference the old namespace
- Modify: `apps/api/RealEstateStar.Api/Program.cs` — update `using` statements
- Modify: All test files referencing the old namespace

- [ ] **Step 1: Move the three Gws files to `Services/Gws/`**

Update the namespace from `RealEstateStar.Api.Features.Cma.Services.Gws` to `RealEstateStar.Api.Services.Gws` in all three files.

- [ ] **Step 2: Update all `using` statements across the solution**

Run: `grep -r "Features.Cma.Services.Gws" apps/api/ --include="*.cs" -l`

Update every file that references the old namespace. Key files:
- `Program.cs`
- `CmaPipeline.cs`
- `SubmitCmaEndpoint.cs`
- All test files referencing GwsService/IGwsService

- [ ] **Step 3: Build and verify**

Run: `dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`
Expected: Build succeeds with 0 errors.

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/RealEstateStar.Api.Tests.csproj`
Expected: All existing tests pass.

- [ ] **Step 4: Add `QueryDriveActivityAsync` to `IGwsService`**

```csharp
// Services/Gws/IGwsService.cs — add new method
Task<string> QueryDriveActivityAsync(
    string agentEmail,
    string ancestorFolder,
    DateTime since,
    CancellationToken ct);
```

And implement in `GwsService.cs`:

```csharp
public async Task<string> QueryDriveActivityAsync(
    string agentEmail, string ancestorFolder, DateTime since, CancellationToken ct)
{
    return await RunGwsAsync(ct,
        "drive", "activity",
        "--user", agentEmail,
        "--ancestor", ancestorFolder,
        "--after", since.ToString("O"),
        "--format", "json");
}
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "refactor: promote IGwsService to shared Services/Gws namespace

Moves IGwsService, GwsService, and LeadBriefData from Features/Cma/Services/Gws
to top-level Services/Gws since both Cma and Leads features depend on it.
Adds QueryDriveActivityAsync for Drive change monitoring."
```

---

## Stream A: Storage Abstractions (no dependencies)

### Task 1: IFileStorageProvider interface

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Storage/IFileStorageProvider.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Storage/IFileStorageProviderContractTests.cs`

- [ ] **Step 1: Write the interface contract tests**

These tests define the behavior contract that ALL providers must satisfy. Use an abstract base class that concrete provider tests inherit.

```csharp
// IFileStorageProviderContractTests.cs
namespace RealEstateStar.Api.Tests.Services.Storage;

public abstract class FileStorageProviderContractTests
{
    protected abstract IFileStorageProvider CreateProvider();

    [Fact]
    public async Task WriteDocument_ThenReadDocument_RoundTrips()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("test-folder", CancellationToken.None);
        await provider.WriteDocumentAsync("test-folder", "doc.md", "# Hello", CancellationToken.None);

        var content = await provider.ReadDocumentAsync("test-folder", "doc.md", CancellationToken.None);

        Assert.Equal("# Hello", content);
    }

    [Fact]
    public async Task ReadDocument_NonExistent_ReturnsNull()
    {
        var provider = CreateProvider();
        var content = await provider.ReadDocumentAsync("no-folder", "no-file.md", CancellationToken.None);
        Assert.Null(content);
    }

    [Fact]
    public async Task UpdateDocument_OverwritesContent()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("test-folder", CancellationToken.None);
        await provider.WriteDocumentAsync("test-folder", "doc.md", "v1", CancellationToken.None);
        await provider.UpdateDocumentAsync("test-folder", "doc.md", "v2", CancellationToken.None);

        var content = await provider.ReadDocumentAsync("test-folder", "doc.md", CancellationToken.None);
        Assert.Equal("v2", content);
    }

    [Fact]
    public async Task DeleteDocument_RemovesFile()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("test-folder", CancellationToken.None);
        await provider.WriteDocumentAsync("test-folder", "doc.md", "content", CancellationToken.None);
        await provider.DeleteDocumentAsync("test-folder", "doc.md", CancellationToken.None);

        var content = await provider.ReadDocumentAsync("test-folder", "doc.md", CancellationToken.None);
        Assert.Null(content);
    }

    [Fact]
    public async Task ListDocuments_ReturnsFileNames()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("list-test", CancellationToken.None);
        await provider.WriteDocumentAsync("list-test", "a.md", "a", CancellationToken.None);
        await provider.WriteDocumentAsync("list-test", "b.md", "b", CancellationToken.None);

        var files = await provider.ListDocumentsAsync("list-test", CancellationToken.None);
        Assert.Contains("a.md", files);
        Assert.Contains("b.md", files);
    }

    [Fact]
    public async Task AppendRow_ThenReadRows_RoundTrips()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("test-sheet", ["col1", "col2", "col3"], CancellationToken.None);

        var rows = await provider.ReadRowsAsync("test-sheet", "col1", "col1", CancellationToken.None);
        Assert.Single(rows);
        Assert.Equal(["col1", "col2", "col3"], rows[0]);
    }

    [Fact]
    public async Task RedactRows_ReplacesMatchingValues()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("redact-test", ["jane@email.com", "data1"], CancellationToken.None);
        await provider.AppendRowAsync("redact-test", ["other@email.com", "data2"], CancellationToken.None);

        await provider.RedactRowsAsync("redact-test", "jane@email.com", "jane@email.com", "[REDACTED]", CancellationToken.None);

        var rows = await provider.ReadRowsAsync("redact-test", "[REDACTED]", "[REDACTED]", CancellationToken.None);
        Assert.Single(rows);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/ --filter "FileStorageProviderContract"`
Expected: FAIL — `IFileStorageProvider` type not found.

- [ ] **Step 3: Write the interface**

```csharp
// Services/Storage/IFileStorageProvider.cs
namespace RealEstateStar.Api.Services.Storage;

public interface IFileStorageProvider
{
    Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct);
    Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct);
    Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct);
    Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct);
    Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct);

    Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct);
    Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct);
    Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct);

    Task EnsureFolderExistsAsync(string folder, CancellationToken ct);
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/
git commit -m "feat: add IFileStorageProvider abstraction with contract tests

Defines the storage abstraction layer that all feature services build on.
Contract tests ensure any provider implementation (GDrive, local, S3, GitHub)
satisfies the same behavioral guarantees."
```

---

### Task 2: LocalStorageProvider (dev fallback)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Storage/LocalStorageProvider.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Storage/LocalStorageProviderTests.cs`

- [ ] **Step 1: Write the concrete test class inheriting contract tests**

```csharp
// LocalStorageProviderTests.cs
namespace RealEstateStar.Api.Tests.Services.Storage;

public class LocalStorageProviderTests : FileStorageProviderContractTests, IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"res-test-{Guid.NewGuid():N}");

    protected override IFileStorageProvider CreateProvider() => new LocalStorageProvider(_tempDir);

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }

    [Fact]
    public async Task WriteDocument_UsesAtomicTempRename()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("atomic-test", CancellationToken.None);
        await provider.WriteDocumentAsync("atomic-test", "file.md", "content", CancellationToken.None);

        // File should exist at final path, no temp files lingering
        var folder = Path.Combine(_tempDir, "atomic-test");
        Assert.Single(Directory.GetFiles(folder));
        Assert.Equal("file.md", Path.GetFileName(Directory.GetFiles(folder)[0]));
    }

    [Fact]
    public async Task WriteDocument_PathTraversal_Throws()
    {
        var provider = CreateProvider();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.WriteDocumentAsync("../escape", "file.md", "bad", CancellationToken.None));
    }

    [Fact]
    public async Task WriteDocument_PathTraversal_DotDotInFileName_Throws()
    {
        var provider = CreateProvider();
        await provider.EnsureFolderExistsAsync("safe", CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.WriteDocumentAsync("safe", "../../escape.md", "bad", CancellationToken.None));
    }

    [Fact]
    public async Task AppendRow_CreatesCsvFile()
    {
        var provider = CreateProvider();
        await provider.AppendRowAsync("my-sheet", ["a", "b", "c"], CancellationToken.None);

        var csvPath = Path.Combine(_tempDir, "logs", "my-sheet.csv");
        Assert.True(File.Exists(csvPath));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/ --filter "LocalStorageProvider"`
Expected: FAIL — `LocalStorageProvider` type not found.

- [ ] **Step 3: Implement LocalStorageProvider**

```csharp
// Services/Storage/LocalStorageProvider.cs
namespace RealEstateStar.Api.Services.Storage;

public class LocalStorageProvider(string basePath) : IFileStorageProvider
{
    public async Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
    {
        var fullPath = ResolveSafePath(folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var tempPath = fullPath + $".tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tempPath, content, ct);
        File.Move(tempPath, fullPath, overwrite: true);
    }

    public async Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var fullPath = ResolveSafePath(folder, fileName);
        if (!File.Exists(fullPath)) return null;
        return await File.ReadAllTextAsync(fullPath, ct);
    }

    public Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => WriteDocumentAsync(folder, fileName, content, ct);

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
    {
        var fullPath = ResolveSafePath(folder, fileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
    {
        var fullPath = ResolveSafeFolderPath(folder);
        if (!Directory.Exists(fullPath)) return Task.FromResult(new List<string>());
        var files = Directory.GetFiles(fullPath).Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
        return Task.FromResult(files);
    }

    public async Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
    {
        var csvPath = GetCsvPath(sheetName);
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        var line = string.Join(",", values.Select(v => $"\"{v.Replace("\"", "\"\"")}\""));
        await File.AppendAllTextAsync(csvPath, line + Environment.NewLine, ct);
    }

    public async Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct)
    {
        var csvPath = GetCsvPath(sheetName);
        if (!File.Exists(csvPath)) return [];

        var lines = await File.ReadAllLinesAsync(csvPath, ct);
        return lines
            .Select(ParseCsvLine)
            .Where(row => row.Contains(filterValue))
            .ToList();
    }

    public async Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct)
    {
        var csvPath = GetCsvPath(sheetName);
        if (!File.Exists(csvPath)) return;

        var lines = await File.ReadAllLinesAsync(csvPath, ct);
        var updated = lines.Select(line =>
            line.Contains(filterValue) ? line.Replace(filterValue, redactedMarker) : line
        ).ToArray();
        await File.WriteAllLinesAsync(csvPath, updated, ct);
    }

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
    {
        var fullPath = ResolveSafeFolderPath(folder);
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string folder, string fileName)
    {
        ValidatePathComponent(folder);
        ValidatePathComponent(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, folder, fileName));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path traversal detected: {folder}/{fileName}");
        return fullPath;
    }

    private string ResolveSafeFolderPath(string folder)
    {
        ValidatePathComponent(folder);
        var fullPath = Path.GetFullPath(Path.Combine(basePath, folder));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path traversal detected: {folder}");
        return fullPath;
    }

    private static void ValidatePathComponent(string component)
    {
        if (component.Contains(".."))
            throw new ArgumentException($"Path component contains '..': {component}");
    }

    private string GetCsvPath(string sheetName) =>
        Path.Combine(basePath, "logs", $"{sheetName}.csv");

    private static List<string> ParseCsvLine(string line) =>
        line.Split(',').Select(v => v.Trim('"').Replace("\"\"", "\"")).ToList();
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/ --filter "LocalStorageProvider"`
Expected: All pass including inherited contract tests.

- [ ] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat: add LocalStorageProvider for dev-mode file storage

Implements IFileStorageProvider using local filesystem with atomic
write-to-temp-then-rename, path traversal protection, and CSV-based
spreadsheet operations. Used when gws CLI is not configured."
```

---

### Task 3: GDriveStorageProvider

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/Storage/GDriveStorageProvider.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/Storage/GDriveStorageProviderTests.cs`

- [ ] **Step 1: Write unit tests (mock IGwsService)**

Test each method delegates correctly to `IGwsService`. Do NOT inherit contract tests here (those require a real filesystem; GDrive tests mock the gws CLI layer).

```csharp
namespace RealEstateStar.Api.Tests.Services.Storage;

public class GDriveStorageProviderTests
{
    private readonly Mock<IGwsService> _gws = new();
    private readonly GDriveStorageProvider _sut;

    public GDriveStorageProviderTests()
    {
        _sut = new GDriveStorageProvider(_gws.Object, "agent@email.com");
    }

    [Fact]
    public async Task WriteDocumentAsync_DelegatesToCreateDocAsync()
    {
        await _sut.WriteDocumentAsync("folder", "file.md", "content", CancellationToken.None);

        _gws.Verify(g => g.CreateDocAsync("agent@email.com", "folder", "file.md", "content", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ReadDocumentAsync_DelegatesToDownloadDocAsync()
    {
        _gws.Setup(g => g.DownloadDocAsync("agent@email.com", "folder", "file.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var result = await _sut.ReadDocumentAsync("folder", "file.md", CancellationToken.None);

        Assert.Equal("content", result);
    }

    [Fact]
    public async Task DeleteDocumentAsync_DelegatesToDeleteDocAsync()
    {
        await _sut.DeleteDocumentAsync("folder", "file.md", CancellationToken.None);

        _gws.Verify(g => g.DeleteDocAsync("agent@email.com", "folder", "file.md", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AppendRowAsync_DelegatesToAppendSheetRowAsync()
    {
        var values = new List<string> { "a", "b" };
        await _sut.AppendRowAsync("sheet", values, CancellationToken.None);

        _gws.Verify(g => g.AppendSheetRowAsync("agent@email.com", "sheet", values, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task EnsureFolderExistsAsync_DelegatesToCreateDriveFolderAsync()
    {
        await _sut.EnsureFolderExistsAsync("my/folder", CancellationToken.None);

        _gws.Verify(g => g.CreateDriveFolderAsync("agent@email.com", "my/folder", It.IsAny<CancellationToken>()));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/ --filter "GDriveStorageProvider"`
Expected: FAIL — `GDriveStorageProvider` type not found.

- [ ] **Step 3: Implement GDriveStorageProvider**

```csharp
// Services/Storage/GDriveStorageProvider.cs
namespace RealEstateStar.Api.Services.Storage;

public class GDriveStorageProvider(IGwsService gwsService, string agentEmail) : IFileStorageProvider
{
    public Task WriteDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => gwsService.CreateDocAsync(agentEmail, folder, fileName, content, ct);

    public Task<string?> ReadDocumentAsync(string folder, string fileName, CancellationToken ct)
        => gwsService.DownloadDocAsync(agentEmail, folder, fileName, ct);

    public Task UpdateDocumentAsync(string folder, string fileName, string content, CancellationToken ct)
        => gwsService.UpdateDocAsync(agentEmail, folder, fileName, content, ct);

    public Task DeleteDocumentAsync(string folder, string fileName, CancellationToken ct)
        => gwsService.DeleteDocAsync(agentEmail, folder, fileName, ct);

    public Task<List<string>> ListDocumentsAsync(string folder, CancellationToken ct)
        => gwsService.ListFilesAsync(agentEmail, folder, ct);

    public Task AppendRowAsync(string sheetName, List<string> values, CancellationToken ct)
        => gwsService.AppendSheetRowAsync(agentEmail, sheetName, values, ct);

    public async Task<List<List<string>>> ReadRowsAsync(string sheetName, string filterColumn, string filterValue, CancellationToken ct)
    {
        var allRows = await gwsService.ReadSheetAsync(agentEmail, sheetName, ct);
        return allRows.Where(row => row.Contains(filterValue)).ToList();
    }

    public Task RedactRowsAsync(string sheetName, string filterColumn, string filterValue, string redactedMarker, CancellationToken ct)
        => gwsService.UpdateSheetRowsAsync(agentEmail, sheetName, filterColumn, filterValue, redactedMarker, ct);

    public Task EnsureFolderExistsAsync(string folder, CancellationToken ct)
        => gwsService.CreateDriveFolderAsync(agentEmail, folder, ct);
}
```

> **Note:** Some `IGwsService` methods (`DownloadDocAsync`, `UpdateDocAsync`, `DeleteDocAsync`, `ListFilesAsync`, `ReadSheetAsync`, `UpdateSheetRowsAsync`) may not exist yet. Add them to the interface and stub implementations as needed. Each new method needs its own test in `GwsServiceTests`.

- [ ] **Step 4: Run tests**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/ --filter "GDriveStorageProvider"`
Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat: add GDriveStorageProvider delegating to IGwsService

Production storage provider mapping IFileStorageProvider operations
to Google Drive via gws CLI. Includes new IGwsService methods for
document download, update, delete, list, sheet read, and sheet update."
```

---

## Stream B: Domain Models + Mappers (no dependencies)

### Task 4: Lead domain models

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Lead.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/BuyerDetails.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/SellerDetails.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/LeadEnrichment.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/LeadScore.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/LeadPaths.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/LeadStatus.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/LeadTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/LeadPathsTests.cs`

- [ ] **Step 1: Write tests for Lead.FullName and LeadPaths**

```csharp
// LeadTests.cs
namespace RealEstateStar.Api.Tests.Features.Leads;

public class LeadTests
{
    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), AgentId = "test", LeadTypes = ["buying"],
            FirstName = "Jane", LastName = "Doe",
            Email = "j@e.com", Phone = "555", Timeline = "asap"
        };
        Assert.Equal("Jane Doe", lead.FullName);
    }
}

// LeadPathsTests.cs
namespace RealEstateStar.Api.Tests.Features.Leads;

public class LeadPathsTests
{
    [Fact]
    public void LeadFolder_ConstructsCorrectPath()
        => Assert.Equal("Real Estate Star/1 - Leads/Jane Doe", LeadPaths.LeadFolder("Jane Doe"));

    [Fact]
    public void LeadFile_ConstructsCorrectPath()
        => Assert.Equal("Real Estate Star/1 - Leads/Jane Doe/Lead Profile.md", LeadPaths.LeadFile("Jane Doe"));

    [Fact]
    public void EnrichmentFile_ConstructsCorrectPath()
        => Assert.Equal("Real Estate Star/1 - Leads/Jane Doe/Research & Insights.md", LeadPaths.EnrichmentFile("Jane Doe"));

    [Fact]
    public void HomeSearchFile_IncludesDate()
    {
        var date = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(
            "Real Estate Star/1 - Leads/Jane Doe/Home Search/2026-03-19-Home Search Results.md",
            LeadPaths.HomeSearchFile("Jane Doe", date));
    }

    [Fact]
    public void CmaFolder_IncludesAddress()
        => Assert.Equal(
            "Real Estate Star/1 - Leads/Jane Doe/123 Main St",
            LeadPaths.CmaFolder("Jane Doe", "123 Main St"));

    [Fact]
    public void Constants_MatchExpectedValues()
    {
        Assert.Equal("Real Estate Star", LeadPaths.Root);
        Assert.Equal("Real Estate Star/1 - Leads", LeadPaths.LeadsFolder);
        Assert.Equal("Real Estate Star/Marketing Consent Log", LeadPaths.ConsentLogSheet);
        Assert.Equal("Real Estate Star/Deletion Audit Log", LeadPaths.DeletionLogSheet);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement all domain models**

Create each file per the spec. Key classes: `Lead`, `BuyerDetails`, `SellerDetails`, `LeadEnrichment`, `LeadScore`, `ScoreFactor`, `LeadPaths`, `LeadStatus` (enum: `Received`, `Enriching`, `Enriched`, `EnrichmentFailed`, `Notified`, `NotificationFailed`, `CmaComplete`, `SearchComplete`, `Complete`).

`LeadEnrichment` should include a static `Empty()` factory method. `LeadScore` should include a static `Default(string reason)` factory method.

- [ ] **Step 4: Run tests**

Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat: add Leads feature domain models and LeadPaths

Lead, BuyerDetails, SellerDetails, LeadEnrichment, LeadScore, LeadPaths,
LeadStatus. All path construction centralized in LeadPaths using constants
matching the existing DriveFolderInitializer numbered hierarchy."
```

---

### Task 5: LeadMarkdownRenderer

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/LeadMarkdownRenderer.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/LeadMarkdownRendererTests.cs`

- [ ] **Step 1: Write tests for RenderLeadProfile**

Test: output contains YAML frontmatter with `leadId`, `status`, `leadTypes`. Test: output contains human-readable sections (`# Jane Doe`, `## Contact`, `## Selling`, `## Buying`). Test: phone formatting (`5551234567` → `(555) 123-4567`). Test: currency formatting (`300000` → `$300,000`). Test: timeline formatting (`1-3months` → `1–3 months`). Test: buyer-only lead omits selling section. Test: seller-only lead omits buying section. Test: notes section rendered when present. Test: notes section omitted when null.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement RenderLeadProfile**

Per spec — YAML frontmatter with system + indexable sections, then clean markdown body.

- [ ] **Step 4: Write tests for RenderResearchInsights**

Test: output contains score display (`Lead Score: 82 / 100`). Test: output contains motivation section. Test: output contains conversation starters. Test: output contains professional background. Test: output contains footer attribution. Test: empty enrichment produces minimal document.

- [ ] **Step 5: Implement RenderResearchInsights**

- [ ] **Step 6: Write tests for RenderHomeSearchResults**

Test: output contains search criteria header. Test: output contains listing cards with address, price, beds/baths. Test: output contains "Why this fits" notes. Test: empty listing list produces "No listings found" message.

- [ ] **Step 7: Implement RenderHomeSearchResults**

- [ ] **Step 8: Run all renderer tests**

Run: `dotnet test apps/api/RealEstateStar.Api.Tests/ --filter "LeadMarkdownRenderer"`
Expected: All pass.

- [ ] **Step 9: Commit**

```bash
git add apps/api/
git commit -m "feat: add LeadMarkdownRenderer for human-readable Drive documents

Renders Lead Profile.md, Research & Insights.md, and Home Search Results.md
with YAML frontmatter (system + indexable fields) and clean markdown body.
Formats phone, currency, timeline for non-technical agents."
```

---

### Task 6: YamlFrontmatterParser

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/YamlFrontmatterParser.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/YamlFrontmatterParserTests.cs`

- [ ] **Step 1: Write tests**

Test: `Parse` extracts key-value pairs from frontmatter between `---` fences. Test: `Parse` returns empty dict for content with no frontmatter. Test: `Parse` handles multi-line frontmatter. Test: `UpdateField` replaces existing field value. Test: `UpdateField` adds new field if not present. Test: `UpdateField` preserves markdown body unchanged. Test: handles YAML arrays (`tags: [a, b, c]`). Test: handles quoted strings.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement YamlFrontmatterParser**

Use `YamlDotNet` NuGet package. Two static methods:
- `Parse(string content)` → `Dictionary<string, string>`
- `UpdateField(string content, string key, string value)` → `string`

- [ ] **Step 4: Run tests**

Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat: add YamlFrontmatterParser for reading/updating lead markdown

Parses YAML frontmatter from markdown documents and updates individual
fields without disturbing the markdown body. Used for lead status updates
and Drive change monitor."
```

---

### Task 7: LeadMappers + Submit DTOs

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/LeadMappers.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadResponse.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/MarketingConsentRequest.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/LeadMappersTests.cs`

- [ ] **Step 1: Write tests**

Test: `ToLead` maps all fields from `SubmitLeadRequest` to `Lead`. Test: `ToLead` generates new GUID. Test: `ToLead` sets `ReceivedAt` to UtcNow. Test: `ToCmaLead` maps seller fields to `Features.Cma.Lead`. Test: `ToCmaLead` maps timeline correctly (`asap` → `ASAP`, `1-3months` → `1-3 months`, etc.). Test: `ToSearchCriteria` maps buyer fields to `HomeSearchCriteria`. Test: `FromFrontmatter` reconstructs `Lead` from frontmatter dictionary.

- [ ] **Step 2: Implement SubmitLeadRequest with DataAnnotations**

```csharp
namespace RealEstateStar.Api.Features.Leads.Submit;

public class SubmitLeadRequest
{
    [Required] public required List<string> LeadTypes { get; init; }
    [Required, StringLength(100)] public required string FirstName { get; init; }
    [Required, StringLength(100)] public required string LastName { get; init; }
    [Required, StringLength(254), EmailAddress] public required string Email { get; init; }
    [Required, StringLength(30), RegularExpression(@"^\+?[\d\s\-().]{7,20}$")] public required string Phone { get; init; }
    [Required] public required string Timeline { get; init; }
    public BuyerDetailsRequest? Buyer { get; init; }
    public SellerDetailsRequest? Seller { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
    [Required] public required MarketingConsentRequest MarketingConsent { get; init; }
}

public class BuyerDetailsRequest
{
    [Required, StringLength(200)] public required string DesiredArea { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBeds { get; init; }
    public int? MinBaths { get; init; }
    public string? PreApproved { get; init; }
    public decimal? PreApprovalAmount { get; init; }
}

public class SellerDetailsRequest
{
    [Required, StringLength(300)] public required string Address { get; init; }
    [Required, StringLength(100)] public required string City { get; init; }
    [Required, StringLength(2)] public required string State { get; init; }
    [Required, RegularExpression(@"^\d{5}(-\d{4})?$")] public required string Zip { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? Sqft { get; init; }
}

public class MarketingConsentRequest
{
    [Required] public required bool OptedIn { get; init; }
    [Required] public required string ConsentText { get; init; }
    [Required] public required List<string> Channels { get; init; }
}
```

- [ ] **Step 3: Implement LeadMappers**

- [ ] **Step 4: Run tests**

Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat: add LeadMappers and SubmitLead DTOs

Maps SubmitLeadRequest to Lead domain, CMA Lead, and HomeSearchCriteria.
Request DTO includes DataAnnotations for validation. Timeline mapping
matches existing CmaJob.GetReportType expectations."
```

---

## Stream C: Frontend (Agent Site) — parallel with A and B

### Task 10: Turnstile widget + honeypot in LeadForm

**Files:**
- Modify: `packages/ui/LeadForm/LeadForm.tsx`
- Modify: `packages/ui/LeadForm/LeadForm.test.tsx`
- Create: `apps/agent-site/lib/turnstile.ts`
- Test: `apps/agent-site/lib/__tests__/turnstile.test.ts`

- [ ] **Step 1: Install Turnstile package**

Run: `npm install @cloudflare/turnstile-widget --save --prefix apps/agent-site`

- [ ] **Step 2: Write tests for Turnstile server-side validation**

```typescript
// turnstile.test.ts
describe("validateTurnstile", () => {
  it("returns true when Cloudflare responds with success", async () => { /* mock fetch */ });
  it("returns false when Cloudflare responds with failure", async () => { /* mock fetch */ });
  it("returns false when fetch throws", async () => { /* mock fetch rejection */ });
});
```

- [ ] **Step 3: Implement turnstile.ts**

```typescript
// apps/agent-site/lib/turnstile.ts
export async function validateTurnstile(token: string): Promise<boolean> {
  const secret = process.env.TURNSTILE_SECRET_KEY;
  if (!secret) return false;

  try {
    const res = await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({ secret, response: token }),
    });
    const data = await res.json();
    return data.success === true;
  } catch {
    return false;
  }
}
```

- [ ] **Step 4: Add honeypot field to LeadForm**

Add hidden `website` field with `aria-hidden="true"`, `tabIndex={-1}`, `position: absolute; left: -9999px`. Add Turnstile widget with `onSuccess` callback. Disable submit button until Turnstile token received.

- [ ] **Step 5: Write LeadForm tests**

Test: honeypot field is hidden (`aria-hidden`). Test: Turnstile widget renders. Test: submit button disabled without Turnstile token. Test: marketing consent checkbox is required.

- [ ] **Step 6: Run tests**

Run: `npm test --prefix packages/ui -- --run`
Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add packages/ui/ apps/agent-site/lib/
git commit -m "feat: add Turnstile bot protection and honeypot to LeadForm

Invisible Cloudflare Turnstile challenge + honeypot hidden field.
Server-side Turnstile validation in lib/turnstile.ts.
Submit disabled until challenge passes."
```

---

### Task 11: Server action + HMAC signing

**Files:**
- Create: `apps/agent-site/actions/submit-lead.ts`
- Create: `apps/agent-site/lib/hmac.ts`
- Modify: `apps/agent-site/components/sections/shared/CmaSection.tsx`
- Test: `apps/agent-site/lib/__tests__/hmac.test.ts`
- Test: `apps/agent-site/__tests__/actions/submit-lead.test.ts`

- [ ] **Step 1: Write HMAC signing tests**

Test: `signRequest` produces `sha256=` prefixed hex string. Test: same input produces same signature. Test: different input produces different signature. Test: timestamp is included in signed message.

- [ ] **Step 2: Implement hmac.ts**

```typescript
// apps/agent-site/lib/hmac.ts
export async function signAndForward(agentId: string, body: string): Promise<Response> {
  const apiKey = process.env.LEAD_API_KEY!;
  const hmacSecret = process.env.LEAD_HMAC_SECRET!;
  const apiUrl = process.env.LEAD_API_URL!;
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const message = `${timestamp}.${body}`;

  const key = await crypto.subtle.importKey(
    "raw", new TextEncoder().encode(hmacSecret),
    { name: "HMAC", hash: "SHA-256" }, false, ["sign"]
  );
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
  const signature = `sha256=${Array.from(new Uint8Array(sig)).map(b => b.toString(16).padStart(2, "0")).join("")}`;

  return fetch(`${apiUrl}/agents/${agentId}/leads`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": apiKey,
      "X-Signature": signature,
      "X-Timestamp": timestamp,
    },
    body,
  });
}
```

- [ ] **Step 3: Write server action tests**

Test: honeypot filled → returns fake success (no API call). Test: Turnstile fails → returns error. Test: successful submission → returns leadId. Test: API error → returns generic error message.

- [ ] **Step 4: Implement submit-lead.ts server action**

```typescript
"use server";
import { validateTurnstile } from "@/lib/turnstile";
import { signAndForward } from "@/lib/hmac";

export async function submitLead(agentId: string, formData: any, turnstileToken: string) {
  if (formData.website) return { leadId: "fake-id", status: "received" };

  const isHuman = await validateTurnstile(turnstileToken);
  if (!isHuman) return { error: "Verification failed. Please try again." };

  const body = JSON.stringify(formData);
  const response = await signAndForward(agentId, body);

  if (!response.ok) return { error: "Something went wrong. Please try again." };
  return response.json();
}
```

- [ ] **Step 5: Update CmaSection.tsx**

Replace the existing `if (isSelling)` conditional API call with `submitLead(accountId, leadData, turnstileToken)` for ALL lead types.

- [ ] **Step 6: Run all frontend tests**

Run: `npm test --prefix apps/agent-site -- --run`
Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add apps/agent-site/
git commit -m "feat: server-side lead submission with HMAC signing

All leads now go through Next.js server action → HMAC-signed → .NET API.
Browser never sees API URL. Honeypot silently traps bots.
CmaSection updated to submit all lead types, not just sellers."
```

---

### Task 12: Privacy pages (opt-out, delete, subscribe)

**Files:**
- Create: `apps/agent-site/app/[handle]/privacy/opt-out/page.tsx`
- Create: `apps/agent-site/app/[handle]/privacy/delete/page.tsx`
- Create: `apps/agent-site/app/[handle]/privacy/subscribe/page.tsx`
- Create: `apps/agent-site/actions/privacy.ts`
- Test: `apps/agent-site/__tests__/privacy/opt-out.test.tsx`
- Test: `apps/agent-site/__tests__/privacy/delete.test.tsx`
- Test: `apps/agent-site/__tests__/privacy/subscribe.test.tsx`

- [ ] **Step 1: Write tests for each privacy page**

Test: opt-out page shows confirmation prompt. Test: opt-out page calls server action on confirm. Test: delete page has email input. Test: delete page shows "check your email" after submission. Test: subscribe page shows re-subscribe confirmation. Test: all pages handle error states.

- [ ] **Step 2: Implement privacy server actions**

`apps/agent-site/actions/privacy.ts` — three server actions (`requestOptOut`, `requestDeletion`, `requestSubscribe`) that HMAC-sign and forward to the API.

- [ ] **Step 3: Implement privacy pages**

Simple forms with confirmation UX. Each reads `email` and `token` from URL query params.

- [ ] **Step 4: Run tests**

Expected: All pass.

- [ ] **Step 5: Commit**

```bash
git add apps/agent-site/
git commit -m "feat: add privacy pages for opt-out, deletion, and re-subscribe

GDPR/CCPA compliant privacy endpoints with email verification.
Server actions handle HMAC signing. Pages are minimal forms with
confirmation UX."
```

---

### Task 13: Grafana Faro browser tracing + Cloudflare Worker OTel

**Files:**
- Create: `apps/agent-site/lib/faro.ts`
- Create: `apps/agent-site/__tests__/lib/faro.test.ts`
- Modify: `apps/agent-site/app/layout.tsx` (or equivalent root layout)
- Modify: `apps/agent-site/wrangler.jsonc` — add `nodejs_compat` compatibility flag
- Create: `apps/agent-site/instrumentation.ts` — OTel Worker wrapper

- [ ] **Step 1: Install Faro + Worker OTel packages**

Run: `npm install @grafana/faro-web-sdk @grafana/faro-web-tracing @grafana/faro-react @microlabs/otel-cf-workers --save --prefix apps/agent-site`

- [ ] **Step 2: Create Faro initialization module**

```typescript
// apps/agent-site/lib/faro.ts
import { initializeFaro } from "@grafana/faro-web-sdk";
import { TracingInstrumentation } from "@grafana/faro-web-tracing";

export function initFaro() {
  const url = process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL;
  if (!url) return;

  initializeFaro({
    url,
    app: { name: "agent-site", version: "1.0.0" },
    instrumentations: [
      new TracingInstrumentation({
        instrumentationOptions: {
          propagateTraceHeaderCorsUrls: [/\/api\//],
        },
      }),
    ],
    sessionTracking: { enabled: true },
  });
}
```

- [ ] **Step 3: Write Faro tests**

```typescript
// apps/agent-site/__tests__/lib/faro.test.ts
import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock the Faro SDK
vi.mock("@grafana/faro-web-sdk", () => ({
  initializeFaro: vi.fn(),
}));
vi.mock("@grafana/faro-web-tracing", () => ({
  TracingInstrumentation: vi.fn(),
}));

describe("initFaro", () => {
  beforeEach(() => {
    vi.resetModules();
    delete process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL;
  });

  it("initializes Faro when FARO_COLLECTOR_URL is set", async () => {
    process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL = "https://faro.example.com/collect";
    const { initFaro } = await import("@/lib/faro");
    const { initializeFaro } = await import("@grafana/faro-web-sdk");
    initFaro();
    expect(initializeFaro).toHaveBeenCalledWith(
      expect.objectContaining({ url: "https://faro.example.com/collect" })
    );
  });

  it("does nothing when FARO_COLLECTOR_URL is not set", async () => {
    const { initFaro } = await import("@/lib/faro");
    const { initializeFaro } = await import("@grafana/faro-web-sdk");
    initFaro();
    expect(initializeFaro).not.toHaveBeenCalled();
  });

  it("does not throw when FARO_COLLECTOR_URL is empty string", async () => {
    process.env.NEXT_PUBLIC_FARO_COLLECTOR_URL = "";
    const { initFaro } = await import("@/lib/faro");
    expect(() => initFaro()).not.toThrow();
  });
});
```

- [ ] **Step 4: Add Faro init to root layout**

Call `initFaro()` in a client component wrapper.

- [ ] **Step 5: Add Cloudflare Worker OTel instrumentation**

Per spec — wrap the Next.js handler with `@microlabs/otel-cf-workers` `instrument()`. Add `nodejs_compat` to `wrangler.jsonc` `compatibility_flags`. Configure OTel exporter to send spans to Grafana Cloud OTLP endpoint.

```typescript
// apps/agent-site/instrumentation.ts
import { instrument } from "@microlabs/otel-cf-workers";

export default instrument(handler, {
  service: { name: "agent-site-worker" },
  exporter: {
    url: process.env.OTEL_EXPORTER_OTLP_ENDPOINT,
    headers: { Authorization: `Basic ${process.env.GRAFANA_CLOUD_OTLP_TOKEN}` },
  },
});
```

- [ ] **Step 6: Run tests, commit**

```bash
git add apps/agent-site/
git commit -m "feat: add Grafana Faro + Cloudflare Worker OTel instrumentation

Browser layer: Faro SDK injects W3C traceparent on fetch calls.
Worker layer: @microlabs/otel-cf-workers propagates traces through CF Worker.
Captures Core Web Vitals, JS errors, and session tracking.
Both layers gated on env vars."
```

---

## Stream D: Feature Services (depends on A + B)

### Task 8: ILeadStore + GDriveLeadStore + FileLeadStore

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/ILeadStore.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/GDriveLeadStore.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/FileLeadStore.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/GDriveLeadStoreTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/FileLeadStoreTests.cs`

- [ ] **Step 1: Write ILeadStore interface**

```csharp
public interface ILeadStore
{
    Task SaveAsync(Lead lead, CancellationToken ct);
    Task UpdateEnrichmentAsync(string agentId, Guid leadId, LeadEnrichment enrichment, LeadScore score, CancellationToken ct);
    Task UpdateCmaJobIdAsync(string agentId, Guid leadId, string cmaJobId, CancellationToken ct);
    Task UpdateHomeSearchIdAsync(string agentId, Guid leadId, string homeSearchId, CancellationToken ct);
    Task UpdateStatusAsync(string agentId, Guid leadId, LeadStatus status, CancellationToken ct);
    Task<Lead?> GetAsync(string agentId, Guid leadId, CancellationToken ct);
    Task<Lead?> GetByNameAsync(string agentId, string leadName, CancellationToken ct);
    Task<List<Lead>> ListByStatusAsync(string agentId, LeadStatus status, CancellationToken ct);
    Task DeleteAsync(string agentId, Guid leadId, CancellationToken ct);
}
```

- [ ] **Step 2: Write GDriveLeadStore tests**

Test: `SaveAsync` calls `EnsureFolderExistsAsync` + `WriteDocumentAsync` with rendered markdown. Test: `SaveAsync` checks for duplicate names. Test: `UpdateEnrichmentAsync` renders Research & Insights.md. Test: `UpdateCmaJobIdAsync` reads and updates frontmatter. Test: `GetByNameAsync` parses frontmatter to reconstruct Lead. Test: `DeleteAsync` calls `DeleteDocumentAsync`.

- [ ] **Step 3: Implement GDriveLeadStore**

Uses `IFileStorageProvider`, `LeadMarkdownRenderer`, `YamlFrontmatterParser`, and `LeadPaths`.

- [ ] **Step 4: Write FileLeadStore tests (inherits same behavioral expectations)**

Uses `LocalStorageProvider` under the hood. Test: files written to correct local paths.

- [ ] **Step 5: Implement FileLeadStore**

- [ ] **Step 6: Run tests**

Expected: All pass.

- [ ] **Step 7: Commit**

```bash
git add apps/api/
git commit -m "feat: add ILeadStore with GDrive and local file implementations

GDriveLeadStore renders markdown via LeadMarkdownRenderer and stores in
Google Drive. FileLeadStore mirrors same structure locally for dev.
Both use IFileStorageProvider abstraction."
```

---

### Task 9: IMarketingConsentLog

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/IMarketingConsentLog.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/MarketingConsentLog.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/MarketingConsent.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/MarketingConsentLogTests.cs`

- [ ] **Step 1: Write tests**

Test: `RecordConsentAsync` appends row with all 10 columns. Test: row values match consent fields. Test: timestamp is ISO 8601 UTC.

- [ ] **Step 2: Implement**

Delegates to `IFileStorageProvider.AppendRowAsync(LeadPaths.ConsentLogSheet, ...)`.

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add IMarketingConsentLog for consent audit trail"
```

---

### Task 9b: IDeletionAuditLog

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/IDeletionAuditLog.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/DeletionAuditLog.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DeletionAuditEntry.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/DeletionAuditLogTests.cs`

**Dependencies:** Tasks 1-3 (IFileStorageProvider).

- [ ] **Step 1: Write tests**

```csharp
// DeletionAuditLogTests.cs
public class DeletionAuditLogTests
{
    private readonly Mock<IFileStorageProvider> _storage = new();
    private readonly DeletionAuditLog _sut;

    public DeletionAuditLogTests()
    {
        _sut = new DeletionAuditLog(_storage.Object);
    }

    [Fact]
    public async Task RecordInitiationAsync_AppendsRowWithRequiredFields()
    {
        await _sut.RecordInitiationAsync("agent1", Guid.NewGuid(), "john@test.com", CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.Is<string>(p => p.Contains("Deletion Audit Log")),
            It.Is<string[]>(r =>
                r.Length >= 5 &&
                r[0] != null &&       // timestamp
                r[1] == "agent1" &&   // agentId
                r[3] == "john@test.com" && // email
                r[4] == "initiated"),  // action
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordCompletionAsync_AppendsRowWithCompletedAction()
    {
        await _sut.RecordCompletionAsync("agent1", Guid.NewGuid(), CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.IsAny<string>(),
            It.Is<string[]>(r => r[4] == "completed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordCompletionAsync_RedactsEmailField()
    {
        await _sut.RecordCompletionAsync("agent1", Guid.NewGuid(), CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.IsAny<string>(),
            It.Is<string[]>(r => r[3] == "[REDACTED]"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordInitiationAsync_TimestampIsIso8601Utc()
    {
        await _sut.RecordInitiationAsync("agent1", Guid.NewGuid(), "a@b.com", CancellationToken.None);
        _storage.Verify(s => s.AppendRowAsync(
            It.IsAny<string>(),
            It.Is<string[]>(r => DateTime.TryParse(r[0], out _)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Write interface and implementation**

```csharp
// IDeletionAuditLog.cs
public interface IDeletionAuditLog
{
    Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct);
    Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct);
}

// DeletionAuditLog.cs
public sealed class DeletionAuditLog(IFileStorageProvider storage) : IDeletionAuditLog
{
    public Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct) =>
        storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), email, "initiated"],
            ct);

    public Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct) =>
        storage.AppendRowAsync(
            LeadPaths.DeletionAuditLogSheet(agentId),
            [DateTime.UtcNow.ToString("o"), agentId, leadId.ToString(), "[REDACTED]", "completed"],
            ct);
}
```

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add IDeletionAuditLog for GDPR/CCPA deletion audit trail

Records initiation and completion events to the Deletion Audit Log sheet.
Email is redacted on completion to comply with right-to-erasure."
```

---

### Task 16: ILeadNotifier + MultiChannelLeadNotifier

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/ILeadNotifier.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/MultiChannelLeadNotifier.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/LeadChatCardRenderer.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/MultiChannelLeadNotifierTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/LeadChatCardRendererTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// MultiChannelLeadNotifierTests.cs — mock setup pattern
public class MultiChannelLeadNotifierTests
{
    private readonly Mock<IHttpClientFactory> _httpFactory = new();
    private readonly Mock<IGwsService> _gws = new();
    private readonly Mock<IAgentConfigService> _agentConfig = new();
    private readonly Mock<ILogger<MultiChannelLeadNotifier>> _logger = new();
    private readonly MultiChannelLeadNotifier _sut;
    private readonly MockHttpMessageHandler _chatHandler = new();

    public MultiChannelLeadNotifierTests()
    {
        _httpFactory.Setup(f => f.CreateClient("GoogleChat"))
            .Returns(new HttpClient(_chatHandler));
        _sut = new MultiChannelLeadNotifier(_httpFactory.Object, _gws.Object,
            _agentConfig.Object, _logger.Object);
    }

    [Fact]
    public async Task NotifyAgentAsync_SendsChatWebhook_WhenUrlConfigured()
    {
        _agentConfig.Setup(a => a.GetAsync("agent1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.MakeAgentConfig(chatWebhookUrl: "https://chat.googleapis.com/v1/spaces/xxx"));
        await _sut.NotifyAgentAsync("agent1", TestData.MakeLead(), CancellationToken.None);
        Assert.Single(_chatHandler.Requests);
    }
}
```

Test: sends Google Chat webhook when URL configured. Test: sends Gmail email always. Test: Chat failure does NOT throw — logs warning, continues to email. Test: email failure logs error but does NOT throw. Test: both channels fire independently. Test: ChatCardRenderer produces valid JSON with header, sections, buttons. Test: email subject includes motivation category and score. Test: email body includes cold call openers section. Test: email body omits buyer section for seller-only leads. Test: email body omits seller section for buyer-only leads. Test: WhatsApp communication goes to the AGENT (not the buyer/seller).

- [ ] **Step 2: Implement LeadChatCardRenderer**

Static method `RenderNewLeadCard(Lead lead)` → returns the Google Chat card JSON object per spec.

- [ ] **Step 3: Implement MultiChannelLeadNotifier**

Per spec — try Chat webhook first (if configured), then always send email. Each channel in its own try/catch with unique log codes `[LEAD-033]` / `[LEAD-034]` / `[LEAD-003]` / `[LEAD-005]`.

- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat: add MultiChannelLeadNotifier with Chat webhook + Gmail

Google Chat card notification with rich lead profile + action buttons.
Gmail email as permanent record fallback. Each channel independent —
one failure doesn't block others."
```

---

### Task 17: ILeadEnricher + ScraperLeadEnricher

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/Enrichment/ILeadEnricher.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/Enrichment/ScraperLeadEnricher.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/Enrichment/ScraperLeadEnricherTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// ScraperLeadEnricherTests.cs — mock setup pattern
public class ScraperLeadEnricherTests
{
    private readonly Mock<IHttpClientFactory> _httpFactory = new();
    private readonly Mock<IClaudeApiService> _claude = new();
    private readonly Mock<ILogger<ScraperLeadEnricher>> _logger = new();
    private readonly MockHttpMessageHandler _scraperHandler = new();
    private readonly ScraperLeadEnricher _sut;

    public ScraperLeadEnricherTests()
    {
        _httpFactory.Setup(f => f.CreateClient("ScraperAPI"))
            .Returns(new HttpClient(_scraperHandler));
        _sut = new ScraperLeadEnricher(_httpFactory.Object, _claude.Object, _logger.Object);
    }

    [Fact]
    public async Task EnrichAsync_FiresAllScrapingQueriesInParallel()
    {
        _scraperHandler.RespondWith("{}"); // all sources return empty
        _claude.Setup(c => c.SendAsync(It.IsAny<ClaudeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestData.MakeClaudeEnrichmentResponse());
        var result = await _sut.EnrichAsync(TestData.MakeLead(), CancellationToken.None);
        Assert.True(_scraperHandler.Requests.Count >= 8, "Should fire 8+ parallel scrape queries");
    }
}
```

Test: fires all scraping queries in parallel. Test: handles partial results (some sources timeout). Test: sends combined data to Claude API. Test: parses Claude response into `LeadEnrichment` + `LeadScore`. Test: strips markdown code fences from Claude response. Test: graceful degradation when Claude unavailable — returns `LeadEnrichment.Empty()` + `LeadScore.Default()`. Test: graceful degradation when all scrapers fail. Test: logs token usage with `[LEAD-026]`. Test: XML-wraps user data for prompt injection prevention.

- [ ] **Step 2: Implement ScraperLeadEnricher**

Build search queries per source table in spec → fire all via `Task.WhenAll` with per-source 5s timeouts → combine with XML source labels → send to Claude with motivation-focused system prompt → parse JSON response → return enrichment + score.

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add ScraperLeadEnricher for lead enrichment pipeline

Scrapes 8+ public data sources in parallel, sends to Claude for
motivation analysis + scoring + cold call opener generation.
Graceful degradation when any source or Claude is unavailable."
```

---

### Task 18: IHomeSearchProvider + ScraperHomeSearchProvider + BuyerListingEmailRenderer

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/IHomeSearchProvider.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/ScraperHomeSearchProvider.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/BuyerListingEmailRenderer.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/HomeSearchCriteria.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Listing.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/ScraperHomeSearchProviderTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/BuyerListingEmailRendererTests.cs`

- [ ] **Step 1: Write search tests**

Test: searches Zillow + Redfin + public MLS in parallel. Test: deduplicates listings by address. Test: sends listings to Claude for curation. Test: Claude adds personalized "Why this fits" notes. Test: handles scraping failures gracefully. Test: respects search criteria filters.

- [ ] **Step 2: Implement ScraperHomeSearchProvider**

Per spec — scrape listing sources, deduplicate, send to Claude for top 5-10 curation with per-listing notes.

- [ ] **Step 3: Write buyer email tests**

```csharp
// BuyerListingEmailRendererTests.cs
public class BuyerListingEmailRendererTests
{
    [Fact]
    public void RenderEmail_SubjectIncludesListingCount()
    {
        var listings = new[] { TestData.MakeListing(), TestData.MakeListing() };
        var result = BuyerListingEmailRenderer.Render("Jane", listings, TestData.MakeAgentConfig());
        Assert.Contains("2 Homes", result.Subject);
    }

    [Fact]
    public void RenderEmail_BodyIncludesPersonalizedIntroFromEnrichment()
    {
        var listings = new[] { TestData.MakeListing() };
        var result = BuyerListingEmailRenderer.Render("Jane", listings,
            TestData.MakeAgentConfig(), enrichment: TestData.MakeEnrichment(motivationCategory: "relocation"));
        Assert.Contains("relocation", result.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderEmail_IncludesListingCardsWithWhyThisFitsNotes()
    {
        var listing = TestData.MakeListing(whyThisFits: "Close to top-rated schools");
        var result = BuyerListingEmailRenderer.Render("Jane", [listing], TestData.MakeAgentConfig());
        Assert.Contains("Close to top-rated schools", result.Body);
    }

    [Fact]
    public void RenderEmail_IncludesAgentSignOff()
    {
        var result = BuyerListingEmailRenderer.Render("Jane", [TestData.MakeListing()],
            TestData.MakeAgentConfig(agentName: "Jenise Buckalew"));
        Assert.Contains("Jenise Buckalew", result.Body);
    }

    [Fact]
    public void RenderEmail_IncludesOptOutFooter()
    {
        var result = BuyerListingEmailRenderer.Render("Jane", [TestData.MakeListing()], TestData.MakeAgentConfig());
        Assert.Contains("unsubscribe", result.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderEmail_IncludesAgentOfficeAddress()
    {
        var result = BuyerListingEmailRenderer.Render("Jane", [TestData.MakeListing()],
            TestData.MakeAgentConfig(officeAddress: "123 Main St, Anytown NJ"));
        Assert.Contains("123 Main St", result.Body);
    }
}
```

- [ ] **Step 4: Implement BuyerListingEmailRenderer**

Static `Render(name, listings, config, enrichment?)` method returning `(Subject, Body)`. Uses agent voice profile for sign-off tone. Includes listing cards with photo placeholder, price, beds/baths, "Why this fits" note, and link. Footer has CAN-SPAM compliant unsubscribe link + agent office address.

- [ ] **Step 5: Run tests, commit**

```bash
git commit -m "feat: add IHomeSearchProvider + buyer listing email renderer

Scrapes Zillow + Redfin + public MLS in parallel, deduplicates by address,
sends to Claude for curation with personalized per-listing notes.
BuyerListingEmailRenderer produces CAN-SPAM compliant listing emails
with personalized intro, per-listing 'Why this fits' notes, and opt-out footer."
```

---

### Task 19: ILeadDataDeletion + GDriveLeadDataDeletion

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/ILeadDataDeletion.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/GDriveLeadDataDeletion.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DeleteResult.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/GDriveLeadDataDeletionTests.cs`

- [ ] **Step 1: Write tests**

Test: `InitiateDeletionRequestAsync` generates cryptographically random token. Test: `InitiateDeletionRequestAsync` sends verification email. Test: `InitiateDeletionRequestAsync` records initiation in deletion audit log. Test: `ExecuteDeletionAsync` deletes Lead Profile.md. Test: `ExecuteDeletionAsync` deletes Research & Insights.md. Test: `ExecuteDeletionAsync` deletes home search files. Test: `ExecuteDeletionAsync` redacts consent log rows. Test: `ExecuteDeletionAsync` records completion in deletion audit log. Test: `ExecuteDeletionAsync` rejects expired token. Test: `ExecuteDeletionAsync` rejects invalid token. Test: `ExecuteDeletionAsync` returns 409 for already-deleted lead. Test: email in deletion audit log is redacted after completion.

- [ ] **Step 2: Implement**

Per spec — token generation (128-bit random, stored hashed SHA-256, 24h expiry), verification email via gws CLI, multi-step deletion across Drive + Sheets.

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add GDPR/CCPA data deletion with email verification

Generates single-use verification tokens, deletes lead data across Drive
and Sheets, redacts consent log rows, records audit trail. Prevents
email enumeration with consistent 202 responses."
```

---

## Stream E: Security Middleware (no dependencies)

### Task 14: ApiKeyHmacMiddleware

**Files:**
- Create: `apps/api/RealEstateStar.Api/Infrastructure/ApiKeyHmacMiddleware.cs`
- Create: `apps/api/RealEstateStar.Api/Infrastructure/ApiKeyHmacOptions.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Infrastructure/ApiKeyHmacMiddlewareTests.cs`

- [ ] **Step 1: Write tests**

Test: valid API key + valid HMAC → request passes through. Test: missing API key → 401. Test: invalid API key → 401 with log `[LEAD-019]`. Test: API key agentId mismatch → 401 with log `[LEAD-020]`. Test: missing HMAC signature → 401. Test: invalid HMAC signature → 401 with log `[LEAD-021]`. Test: timestamp > 5 min old → 401 with log `[LEAD-022]`. Test: timestamp in future > 5 min → 401. Test: uses constant-time comparison (`CryptographicOperations.FixedTimeEquals`). Test: request body can still be read by endpoint after middleware (enable buffering).

- [ ] **Step 2: Implement**

Two-phase middleware: Phase 1 validates API key via `IAgentConfigService`, Phase 2 validates HMAC signature + timestamp. Uses `EnableBuffering()` so body can be read twice.

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add ApiKeyHmacMiddleware for server-to-server auth

API Key for caller identity + HMAC-SHA256 for request integrity + timestamp
replay protection. Constant-time signature comparison. Body buffering for
double-read. Unique log codes for each failure mode."
```

---

### Task 15: Rate limiting policies

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Infrastructure/RateLimitingTests.cs`

- [ ] **Step 1: Write tests**

Test: `lead-create` policy allows 20 requests/hour per IP. Test: `deletion-request` policy allows 5 requests/hour per IP. Test: `delete-data` policy allows 10 requests/hour per IP. Test: `lead-opt-out` policy allows 10 requests/hour per IP.

- [ ] **Step 2: Add rate limiting policies to Program.cs**

```csharp
options.AddPolicy("lead-create", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) }));

options.AddPolicy("deletion-request", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1) }));

options.AddPolicy("delete-data", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

options.AddPolicy("lead-opt-out", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
```

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat: add rate limiting policies for lead endpoints"
```

---

## Stream F: Endpoints (depends on D + E)

### Task 20: SubmitLeadEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Submit/SubmitLeadEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Submit/SubmitLeadEndpointTests.cs`

This is the most complex endpoint. Tests should cover all validation, the synchronous portion (save + consent), and the fire-and-forget behavior.

- [ ] **Step 1: Write validation tests**

Test: missing `firstName` → 400. Test: invalid email → 400. Test: invalid phone → 400. Test: `leadTypes` includes `"selling"` but no `seller.address` → 400. Test: `leadTypes` includes `"buying"` but no `buyer.desiredArea` → 400. Test: missing `marketingConsent` → 400. Test: missing `marketingConsent.consentText` → 400. Test: unknown `agentId` → 404. Test: valid request → 202 with leadId.

- [ ] **Step 2: Write behavioral tests**

Test: saves lead to `ILeadStore`. Test: records consent to `IMarketingConsentLog`. Test: extracts IP and UserAgent from HttpContext (not request body). Test: fires enrichment in background (mock `ILeadEnricher`). Test: fires notification in background. Test: triggers CMA pipeline for seller leads. Test: triggers home search for buyer leads. Test: does NOT trigger CMA for buyer-only leads. Test: does NOT trigger home search for seller-only leads.

- [ ] **Step 3: Write error handling tests**

Test: `ILeadStore.SaveAsync` failure → 500. Test: `IMarketingConsentLog` failure → 500. Test: enrichment failure in background → logged `[LEAD-007]`, notification still fires. Test: notification failure in background → logged `[LEAD-005]`, does not throw. Test: CMA failure in background → logged `[LEAD-004]`.

- [ ] **Step 4: Implement SubmitLeadEndpoint**

Per spec — validate, save, consent log, return 202, then `Task.Run` with captured `Activity.Current?.Context` for enrichment → notification → CMA → home search. Each step in its own try/catch with unique log codes.

```csharp
// SubmitLeadEndpoint.cs
namespace RealEstateStar.Api.Features.Leads.Submit;

public class SubmitLeadEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/agents/{agentId}/leads", Handle)
            .RequireRateLimiting("lead-create");

    internal static async Task<IResult> Handle(
        string agentId,
        SubmitLeadRequest request,
        IAgentConfigService agentConfig,
        ILeadStore leadStore,
        IMarketingConsentLog consentLog,
        ILeadEnricher enricher,
        ILeadNotifier notifier,
        IHomeSearchProvider homeSearchProvider,
        ICmaJobStore cmaJobStore,
        ICmaPipeline pipeline,
        HttpContext httpContext,
        ILogger<SubmitLeadEndpoint> logger,
        CancellationToken ct)
    {
        // ... implementation per spec
    }
}
```

- [ ] **Step 5: Run tests**

Expected: All pass.

- [ ] **Step 6: Commit**

```bash
git commit -m "feat: add SubmitLeadEndpoint — unified lead submission

POST /agents/{agentId}/leads handles all lead types. Synchronous: save + consent.
Async fire-and-forget: enrich → notify → CMA (seller) → home search (buyer).
Graceful degradation at every step. Trace context propagated to background tasks."
```

---

### Task 21: OptOutEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/OptOut/OptOutRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/OptOut/OptOutEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/OptOut/OptOutEndpointTests.cs`

- [ ] **Step 1: Write tests**

Test: valid token → 200, updates frontmatter, appends consent log row. Test: invalid token → 200 (no email enumeration). Test: already opted-out → 200 (idempotent). Test: consent log records `opt-out` action with correct source.

- [ ] **Step 2: Implement**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add OptOutEndpoint for marketing opt-out"
```

---

### Task 22: SubscribeEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Subscribe/SubscribeRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Subscribe/SubscribeEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Subscribe/SubscribeEndpointTests.cs`

- [ ] **Step 1: Write tests**

Test: valid token → 200, updates frontmatter, appends consent log row. Test: invalid token → 200. Test: consent log records `opt-in` action with source `re-subscribe`.

- [ ] **Step 2: Implement**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add SubscribeEndpoint for marketing re-subscribe"
```

---

### Task 23: DeleteDataEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DeleteData/DeleteLeadDataRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DeleteData/DeleteLeadDataResponse.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DeleteData/DeleteDataEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/DeleteData/DeleteDataEndpointTests.cs`

- [ ] **Step 1: Write tests**

Test: valid token → 200 with deletedItems list. Test: invalid reason → 400. Test: expired token → 401. Test: email mismatch → 401. Test: lead not found → 404. Test: already deleted → 409. Test: deletes all artifacts (profile, enrichment, home search, CMA).

- [ ] **Step 2: Implement**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add DeleteDataEndpoint for GDPR/CCPA erasure"
```

---

### Task 28: RetryFailedLeadsEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/RetryFailed/RetryFailedLeadsEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/RetryFailed/RetryFailedLeadsEndpointTests.cs`

- [ ] **Step 1: Write tests**

Test: scans leads with `enrichment_failed` status and re-enriches. Test: scans leads with `notification_failed` status and re-notifies. Test: skips leads in other statuses. Test: individual lead failure doesn't block others. Test: returns count of retried + still-failing.

- [ ] **Step 2: Implement**

Per spec — iterates lead folders, reads frontmatter status, re-runs failed step.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add RetryFailedLeadsEndpoint for resumable enrichment"
```

---

### Task 29: RequestDeletionEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/RequestDeletion/RequestDeletionRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/RequestDeletion/RequestDeletionEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/RequestDeletion/RequestDeletionEndpointTests.cs`

- [ ] **Step 1: Write tests**

Test: known email → sends verification email, returns 202. Test: unknown email → returns 202 (no enumeration). Test: verification email contains token link. Test: deletion audit log records initiation.

- [ ] **Step 2: Implement**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add RequestDeletionEndpoint for GDPR deletion initiation"
```

---

## Stream G: Observability (parallel with F)

### Task 24: LeadDiagnostics (ActivitySource + Meters)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Diagnostics/LeadDiagnostics.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Diagnostics/LeadDiagnosticsTests.cs`

- [ ] **Step 1: Write tests**

Test: `ActivitySource` has name `RealEstateStar.Leads`. Test: counters exist for `leads.received`, `leads.enriched`, `leads.enrichment_failed`, `leads.notification_sent`, `leads.notification_failed`, `leads.deleted`. Test: histograms exist for `leads.enrichment_duration_ms`, `leads.notification_duration_ms`, `leads.home_search_duration_ms`, `leads.total_pipeline_duration_ms`. Test: token counters exist for `leads.llm_tokens.input`, `leads.llm_tokens.output`, `leads.llm_cost_usd`.

- [ ] **Step 2: Implement**

Follow existing `CmaDiagnostics` pattern.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add LeadDiagnostics with ActivitySource + Meters"
```

---

### Task 25: LlmCostEstimator

**Files:**
- Create: `apps/api/RealEstateStar.Api/Services/LlmCostEstimator.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Services/LlmCostEstimatorTests.cs`

- [ ] **Step 1: Write tests**

Test: estimates cost for Claude Sonnet model. Test: estimates cost for Claude Haiku model. Test: reads pricing from config. Test: falls back to default pricing when config missing. Test: returns zero for zero tokens.

- [ ] **Step 2: Implement**

Per spec — reads `LlmPricing:{model}:InputPer1M` and `OutputPer1M` from config.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add LlmCostEstimator for token usage tracking"
```

---

### Task 26: Health checks (Google Drive, ScraperAPI, Turnstile)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Health/GoogleDriveHealthCheck.cs`
- Create: `apps/api/RealEstateStar.Api/Health/ScraperApiHealthCheck.cs`
- Create: `apps/api/RealEstateStar.Api/Health/TurnstileHealthCheck.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Health/GoogleDriveHealthCheckTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Health/ScraperApiHealthCheckTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Health/TurnstileHealthCheckTests.cs`
- Modify: `apps/api/RealEstateStar.Api/Program.cs` — register new checks

- [ ] **Step 1: Write tests for each health check**

GoogleDrive: Test: returns Healthy when `ListDocumentsAsync` succeeds. Test: returns Unhealthy when exception thrown.
ScraperAPI: Test: returns Healthy when account endpoint returns 200. Test: returns Degraded when fails.
Turnstile: Test: returns Healthy when secret key config exists. Test: returns Degraded when missing.

- [ ] **Step 2: Implement all three**

Per spec — follow existing `ClaudeApiHealthCheck` pattern.

- [ ] **Step 3: Register in Program.cs**

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<ClaudeApiHealthCheck>("claude_api", tags: ["ready"])
    .AddCheck<GwsCliHealthCheck>("gws_cli", tags: ["ready"])
    .AddCheck<GoogleDriveHealthCheck>("google_drive", tags: ["ready"])
    .AddCheck<ScraperApiHealthCheck>("scraper_api", tags: ["ready"])
    .AddCheck<TurnstileHealthCheck>("turnstile", tags: ["ready"]);
```

- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat: add health checks for Drive, ScraperAPI, and Turnstile"
```

---

### Task 27: Polly retry + circuit breaker policies

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Infrastructure/PollyPolicyTests.cs`

- [ ] **Step 1: Install Polly**

Add `Microsoft.Extensions.Http.Polly` NuGet package.

- [ ] **Step 2: Write tests**

Test: Claude API client retries 3x on transient errors. Test: Claude API circuit breaker opens after 5 failures. Test: ScraperAPI client retries 2x. Test: Google Chat client retries 1x. Test: retry logs `[LEAD-035]`. Test: circuit breaker open logs `[LEAD-036]`. Test: circuit breaker close logs `[LEAD-037]`.

- [ ] **Step 3: Add Polly policies to Program.cs**

Per spec — named HTTP clients with retry + circuit breaker policies.

- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat: add Polly retry and circuit breaker policies

Claude API: 3x exponential retry + breaker (5 failures, 1 min pause).
ScraperAPI: 2x linear retry + breaker (10 failures, 2 min pause).
Google Chat: 1x retry, no breaker. All with unique log codes."
```

---

## Stream H: Drive Change Monitor (depends on F)

### Task 30: DriveChangeMonitor service

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/DriveChangeMonitor.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/Services/DriveActivityParser.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DriveActivityEvent.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Leads/DriveChangeResult.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/DriveChangeMonitorTests.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/Services/DriveActivityParserTests.cs`

- [ ] **Step 1: Write tests for DriveActivityParser**

Test: parses gws CLI JSON output into `DriveActivityEvent[]`. Test: handles empty activity. Test: maps Move, Create, Edit, Delete, Rename actions. Test: extracts destination parent for Move events.

- [ ] **Step 2: Write tests for DriveChangeMonitor**

Test: folder move from `1 - Leads` to `2 - Active Clients` updates status to `active_client`. Test: folder move to `3 - Under Contract` updates status to `under_contract`. Test: folder move to `4 - Closed` updates status to `closed`. Test: folder move to `5 - Inactive` updates status to `inactive`. Test: file delete logs warning `[LEAD-041]`. Test: individual agent failure doesn't block other agents. Test: DriveChangeResult.Merge aggregates counts.

- [ ] **Step 3: Implement**

Per spec — folder-number-to-status mapping, frontmatter status update, event processing loop.

- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat: add DriveChangeMonitor for lead lifecycle tracking

Polls Google Drive via gws CLI, detects folder moves and file changes.
Automatically updates lead frontmatter status based on destination folder.
Foundation for Phase 2 WhatsApp notifications."
```

---

### Task 31: PollDriveChangesEndpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Leads/PollDriveChanges/PollDriveChangesEndpoint.cs`
- Test: `apps/api/RealEstateStar.Api.Tests/Features/Leads/PollDriveChanges/PollDriveChangesEndpointTests.cs`

- [ ] **Step 1: Write tests**

Test: iterates all agents and polls each. Test: individual agent failure logged, continues others. Test: returns aggregate DriveChangeResult. Test: requires `Authorization: Bearer` internal token.

- [ ] **Step 2: Implement**

Per spec — `POST /internal/drive/poll`, iterates agents, calls `DriveChangeMonitor.PollAsync`.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add PollDriveChangesEndpoint for cron-based Drive monitoring"
```

---

## Stream I: Infrastructure (parallel)

### Task 32: DI registration in Program.cs

> **Note:** This task compiles independently but must be completed BEFORE manual or integration testing of any Stream F endpoints. `AddEndpoints` auto-discovers at compile time, but injected services must be registered for runtime.

**Files:**
- Modify: `apps/api/RealEstateStar.Api/Program.cs`

- [ ] **Step 1: Add all new service registrations**

```csharp
// Storage provider — GDrive by default, local fallback for dev
if (builder.Configuration.GetValue<bool>("Storage:UseLocal"))
    builder.Services.AddSingleton<IFileStorageProvider>(sp => new LocalStorageProvider("data"));
else
    builder.Services.AddScoped<IFileStorageProvider, GDriveStorageProvider>();

// Lead feature services
builder.Services.AddScoped<ILeadStore, GDriveLeadStore>();
builder.Services.AddScoped<IMarketingConsentLog, MarketingConsentLog>();
builder.Services.AddScoped<ILeadDataDeletion, GDriveLeadDataDeletion>();
builder.Services.AddScoped<ILeadNotifier, MultiChannelLeadNotifier>();
builder.Services.AddScoped<ILeadEnricher, ScraperLeadEnricher>();
builder.Services.AddScoped<IDeletionAuditLog, DeletionAuditLog>();

// Home search — MLS if configured, scraper fallback
if (builder.Configuration.GetSection("Mls").Exists())
    builder.Services.AddScoped<IHomeSearchProvider, MlsHomeSearchProvider>();
else
    builder.Services.AddScoped<IHomeSearchProvider, ScraperHomeSearchProvider>();

// Drive change monitor
builder.Services.AddScoped<DriveChangeMonitor>();

// OpenAPI
builder.Services.AddOpenApi();
```

- [ ] **Step 2: Add middleware pipeline for lead endpoints**

```csharp
// After app.Build():
app.MapOpenApi();

// Apply HMAC middleware to lead endpoints
// (registered via IEndpoint auto-discovery — middleware applied per-route)
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: register all Lead feature services in DI container"
```

---

### Task 33: CI/CD pipeline updates

**Files:**
- Modify: `.github/workflows/agent-site.yml`
- Modify: `.github/workflows/deploy-agent-site.yml`
- Modify: `.github/workflows/api.yml` (if exists)
- Modify: `.github/workflows/deploy-api.yml`
- Create: `.github/workflows/drive-monitor.yml`

- [ ] **Step 1: Update agent-site workflow with new secrets + path filters**

Add `TURNSTILE_SECRET_KEY`, `TURNSTILE_SITE_KEY`, `LEAD_API_KEY`, `LEAD_HMAC_SECRET`, `LEAD_API_URL`, `FARO_COLLECTOR_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `GRAFANA_CLOUD_OTLP_TOKEN` to build env and wrangler deploy vars. Add `config/agents/**` to path filters so agent config changes trigger builds.

- [ ] **Step 2: Update deploy-api workflow**

Add post-deploy `/health/ready` check (WARN, not fail). Add new secrets: `SCRAPER_API_KEY`, `INTERNAL_API_TOKEN`. Add `config/agents/**` path filter. Add `Storage__UseLocal: true` test env var for test job.

- [ ] **Step 3: Add coverage threshold gate to test jobs**

Add `--threshold 80` flag to coverage step in both api and agent-site workflows (the 100% target is enforced locally — CI gates at 80% to prevent flaky failures from cache/timing issues).

- [ ] **Step 4: Add wrangler secrets deployment step**

After wrangler deploy, run `wrangler secret put` for each new secret: `TURNSTILE_SITE_KEY`, `FARO_COLLECTOR_URL`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `GRAFANA_CLOUD_OTLP_TOKEN`.

- [ ] **Step 5: Add post-deploy Turnstile presence check**

After agent-site deploy, add a curl check verifying the Turnstile script tag is present in the rendered HTML of the form page.

- [ ] **Step 7: Create drive-monitor.yml**

```yaml
name: Drive Change Monitor
on:
  schedule:
    - cron: '*/5 * * * *'
  workflow_dispatch:
jobs:
  poll:
    runs-on: ubuntu-latest
    steps:
      - name: Poll for Drive changes
        run: |
          curl -s -X POST \
            -H "Authorization: Bearer ${{ secrets.INTERNAL_API_TOKEN }}" \
            "https://api.real-estate-star.com/internal/drive/poll"
```

- [ ] **Step 8: Create lead-pipeline-smoke.yml (nightly)**

```yaml
name: Lead Pipeline Smoke Test
on:
  schedule:
    - cron: '0 6 * * *'  # 6am UTC daily
  workflow_dispatch:
jobs:
  smoke:
    runs-on: ubuntu-latest
    steps:
      - name: Check API health
        run: |
          STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://api.real-estate-star.com/health/ready)
          if [ "$STATUS" != "200" ]; then
            echo "::error::API health check failed with status $STATUS"
            exit 1
          fi
      - name: Verify Turnstile on agent site
        run: |
          BODY=$(curl -s https://jenisesellsnj.real-estate-star.com)
          if ! echo "$BODY" | grep -q "challenges.cloudflare.com"; then
            echo "::warning::Turnstile script not found on agent site"
          fi
```

- [ ] **Step 9: Commit**

```bash
git commit -m "ci: update pipelines for lead submission feature

Add new secrets to agent-site and API workflows. Add /health/ready
post-deploy check. Create drive-monitor.yml for cron-based Drive polling."
```

---

### Task 34: OpenAPI + TypeScript client codegen

**Files:**
- Create: `packages/api-client/package.json`
- Create: `packages/api-client/client.ts`
- Create: `packages/api-client/index.ts`

- [ ] **Step 1: Set up api-client package**

```json
{
  "name": "@real-estate-star/api-client",
  "private": true,
  "scripts": {
    "generate": "openapi-typescript http://localhost:5135/openapi/v1.json -o generated/types.ts",
    "generate:ci": "openapi-typescript openapi.json -o generated/types.ts"
  },
  "dependencies": {
    "openapi-fetch": "^0.13.0"
  },
  "devDependencies": {
    "openapi-typescript": "^7.0.0"
  }
}
```

- [ ] **Step 2: Create client wrapper**

```typescript
import createClient from "openapi-fetch";
import type { paths } from "./generated/types";

export function createApiClient(baseUrl: string) {
  return createClient<paths>({ baseUrl });
}
```

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add packages/api-client for OpenAPI type generation

openapi-typescript generates TypeScript types from .NET OpenAPI spec.
openapi-fetch provides type-safe API client. Types auto-generated
from spec — no manual DTO maintenance."
```

---

## Stream J: Legal & Compliance Review

### Task 35: ADA, CCPA, GDPR, TCA legal compliance audit

This task is a **review-only task** — no code changes, but produces a compliance checklist that gates the PR merge.

**Files:**
- Create: `docs/superpowers/plans/2026-03-19-lead-submission-legal-audit.md`

- [ ] **Step 1: ADA (Americans with Disabilities Act) review**

Audit all new frontend components for:
- [ ] Turnstile widget: is `aria-hidden` appropriate? Does it have a fallback for screen readers?
- [ ] Honeypot field: `aria-hidden="true"` set correctly, `tabIndex={-1}` prevents keyboard focus
- [ ] Privacy pages: all forms have proper `<label>` elements, error messages associated with inputs via `aria-describedby`
- [ ] Marketing consent checkbox: labeled, keyboard-operable, focus-visible
- [ ] All new buttons: have accessible names, focus-visible rings
- [ ] Color contrast: score display, status badges meet WCAG 2.1 AA (4.5:1)

- [ ] **Step 2: CCPA (California Consumer Privacy Act) review**

- [ ] Right to Delete: `DELETE /leads/{leadId}/data` endpoint exists with email verification
- [ ] Right to Know: Lead Profile.md in Drive contains all collected data (transparent)
- [ ] Right to Opt-Out: `POST /leads/opt-out` with consent token
- [ ] Non-discrimination: opted-out leads still receive transactional emails (CMA, listings)
- [ ] Verification: email-based verification for deletion requests (not just leadId)
- [ ] Response time: automated deletion within seconds (well under 45-day requirement)
- [ ] Privacy policy link: present in email footers and agent site

- [ ] **Step 3: GDPR (General Data Protection Regulation) review**

- [ ] Lawful basis: consent checkbox with explicit opt-in (not pre-checked)
- [ ] Right to Erasure (Art. 17): full data deletion across Drive, Sheets, CMA artifacts
- [ ] Consent audit trail: Marketing Consent Log captures who, what, when, how
- [ ] Data minimization: only collect what's needed for lead processing
- [ ] Right to object: opt-out link in every email
- [ ] Easy re-subscribe: same ease as opt-out (GDPR requirement)
- [ ] Data portability: frontmatter is machine-readable YAML (parseable by any tool)
- [ ] Deletion audit: separate log records all deletion events
- [ ] Consent log redaction: consent rows redacted (not deleted) to preserve audit trail
- [ ] No PII in logs/telemetry: verified — only leadId/agentId in OTel spans

- [ ] **Step 4: TCA (Telephone Consumer Protection Act) review**

- [ ] Consent for marketing calls: `channels` array includes `phone` option
- [ ] Consent text: explicit mention of phone calls if channel selected
- [ ] DNC compliance: opted-out leads must not receive marketing calls
- [ ] Record keeping: Marketing Consent Log records phone consent separately
- [ ] WhatsApp (Phase 2): business-initiated messages require approved templates

- [ ] **Step 5: CAN-SPAM review**

- [ ] Physical address: agent's office address in email footer (from agent config)
- [ ] Unsubscribe link: present in every lead-facing email
- [ ] Opt-out processing: within 10 business days (we process immediately)
- [ ] No deceptive subject lines: email subjects clearly identify sender
- [ ] Transactional vs marketing: CMA reports and listing emails are transactional (exempt)

- [ ] **Step 6: Document findings and remediation**

Write compliance audit results to `docs/superpowers/plans/2026-03-19-lead-submission-legal-audit.md` with pass/fail per item and any remediation needed.

- [ ] **Step 7: Commit**

```bash
git commit -m "docs: add ADA/CCPA/GDPR/TCA/CAN-SPAM compliance audit

Comprehensive legal review of lead submission feature covering privacy,
accessibility, consent management, and telecommunications compliance."
```

---

## Final: Integration Test + Coverage

### Task 36: Full integration test

**Files:**
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Leads/LeadSubmissionIntegrationTests.cs`

- [ ] **Step 1: Write end-to-end integration test**

Test the complete flow: valid request → save → consent log → enrich → notify → CMA trigger → home search. Use `LocalStorageProvider` and mocked external services (Claude, ScraperAPI, Gmail).

- [ ] **Step 2: Write deletion integration test**

Test: initiate → verify → delete → confirm all artifacts removed → consent log redacted.

- [ ] **Step 3: Run with coverage**

Run: `bash apps/api/scripts/coverage.sh`
Expected: 100% branch coverage on all new code.

- [ ] **Step 4: Fix any coverage gaps**

Use `bash apps/api/scripts/coverage.sh --low-only` to find classes below 100%.

- [ ] **Step 5: Commit**

```bash
git commit -m "test: add integration tests for full lead submission pipeline

End-to-end tests covering submit → enrich → notify → CMA → home search,
and initiate → verify → delete → audit. 100% branch coverage."
```

---

### Task 37: Agent config schema update

**Files:**
- Modify: `config/agent.schema.json`
- Modify: `config/agents/jenise-buckalew.json`

- [ ] **Step 1: Add `voice` and `notifications` fields to schema**

```json
{
  "integrations": {
    "notifications": {
      "chatWebhookUrl": { "type": "string", "format": "uri" },
      "whatsappPhoneId": { "type": "string" },
      "whatsappProvider": { "type": "string", "enum": ["twilio"] }
    },
    "leadApi": {
      "apiKey": { "type": "string" }
    },
    "mls": {
      "provider": { "type": "string" },
      "apiKey": { "type": "string" },
      "boardId": { "type": "string" },
      "agentMlsId": { "type": "string" }
    }
  },
  "voice": {
    "description": { "type": "string" },
    "sampleMessages": { "type": "array", "items": { "type": "string" } },
    "importedFrom": { "type": ["string", "null"] }
  }
}
```

- [ ] **Step 2: Update jenise-buckalew.json with voice config**

Per spec — warm, professional tone with sample messages.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat: add voice, notifications, and leadApi to agent config schema

Agent voice profile for Claude-drafted communications. Google Chat
webhook URL for instant notifications. Per-agent API key for
server-to-server auth."
```

---

## Stream K: Platform Status Page

### Task 38: Status page on platform.real-estate-star.com

The platform needs a public status page that shows the health of the entire Real Estate Star service stack. This page calls the API's `/health/ready` endpoint and displays per-dependency status. It also displays the health of the platform itself (Next.js) and the agent site (Cloudflare Workers).

**Files:**
- Create: `apps/platform/app/status/page.tsx`
- Create: `apps/platform/app/status/StatusDashboard.tsx`
- Create: `apps/platform/app/status/useHealthCheck.ts`
- Create: `apps/platform/__tests__/status/StatusDashboard.test.tsx`
- Create: `apps/platform/__tests__/status/page.test.tsx`
- Modify: `apps/platform/app/layout.tsx` — add "Status" link to footer/nav

**Dependencies:** Task 26 (health checks exist in the API). Can start earlier with mocked API responses.

- [ ] **Step 1: Write failing test for StatusDashboard**

```tsx
// apps/platform/__tests__/status/StatusDashboard.test.tsx
import { render, screen, waitFor } from "@testing-library/react";
import { StatusDashboard } from "@/app/status/StatusDashboard";

const mockHealthy = {
  status: "Healthy",
  entries: {
    "claude-api": { status: "Healthy", duration: "00:00:00.234" },
    "gws-cli": { status: "Healthy", duration: "00:00:00.012" },
    "google-drive": { status: "Healthy", duration: "00:00:00.456" },
    "scraper-api": { status: "Healthy", duration: "00:00:00.789" },
    "turnstile": { status: "Healthy", duration: "00:00:00.100" },
  },
};

const mockDegraded = {
  status: "Degraded",
  entries: {
    "claude-api": { status: "Healthy", duration: "00:00:00.234" },
    "gws-cli": { status: "Degraded", duration: "00:00:05.000",
      description: "gws CLI not found" },
  },
};

beforeEach(() => {
  global.fetch = vi.fn();
});

it("renders all healthy services with green indicators", async () => {
  (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
    ok: true, json: async () => mockHealthy,
  });
  render(<StatusDashboard />);
  await waitFor(() => {
    expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
  });
  expect(screen.getByTestId("status-claude-api")).toHaveAttribute(
    "data-status", "Healthy"
  );
});

it("renders degraded status with yellow indicator and description", async () => {
  (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
    ok: true, json: async () => mockDegraded,
  });
  render(<StatusDashboard />);
  await waitFor(() => {
    expect(screen.getByText("Degraded Performance")).toBeInTheDocument();
  });
  expect(screen.getByText("gws CLI not found")).toBeInTheDocument();
});

it("renders error state when API is unreachable", async () => {
  (fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error("Network error"));
  render(<StatusDashboard />);
  await waitFor(() => {
    expect(screen.getByText("Unable to reach API")).toBeInTheDocument();
  });
});

it("shows loading state initially", () => {
  (fetch as ReturnType<typeof vi.fn>).mockReturnValueOnce(new Promise(() => {}));
  render(<StatusDashboard />);
  expect(screen.getByTestId("status-loading")).toBeInTheDocument();
});

it("displays response time for each service", async () => {
  (fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
    ok: true, json: async () => mockHealthy,
  });
  render(<StatusDashboard />);
  await waitFor(() => {
    expect(screen.getByText("234ms")).toBeInTheDocument();
  });
});

it("auto-refreshes every 30 seconds", async () => {
  vi.useFakeTimers();
  (fetch as ReturnType<typeof vi.fn>)
    .mockResolvedValueOnce({ ok: true, json: async () => mockHealthy })
    .mockResolvedValueOnce({ ok: true, json: async () => mockDegraded });
  render(<StatusDashboard />);
  await waitFor(() => {
    expect(screen.getByText("All Systems Operational")).toBeInTheDocument();
  });
  vi.advanceTimersByTime(30_000);
  await waitFor(() => {
    expect(screen.getByText("Degraded Performance")).toBeInTheDocument();
  });
  vi.useRealTimers();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm run test --prefix apps/platform -- --run status/StatusDashboard`
Expected: FAIL — module not found

- [ ] **Step 3: Write useHealthCheck hook**

```tsx
// apps/platform/app/status/useHealthCheck.ts
"use client";
import { useState, useEffect, useCallback } from "react";

interface HealthEntry {
  status: "Healthy" | "Degraded" | "Unhealthy";
  duration?: string;
  description?: string;
}

export interface HealthResponse {
  status: "Healthy" | "Degraded" | "Unhealthy";
  entries: Record<string, HealthEntry>;
}

export type FetchState =
  | { kind: "loading" }
  | { kind: "success"; data: HealthResponse }
  | { kind: "error"; message: string };

const REFRESH_INTERVAL = 30_000;

export function useHealthCheck(apiUrl: string): FetchState {
  const [state, setState] = useState<FetchState>({ kind: "loading" });

  const fetchHealth = useCallback(async () => {
    try {
      const res = await fetch(`${apiUrl}/health/ready`, {
        cache: "no-store",
      });
      if (!res.ok) {
        setState({ kind: "error", message: `API returned ${res.status}` });
        return;
      }
      const data: HealthResponse = await res.json();
      setState({ kind: "success", data });
    } catch (err) {
      setState({
        kind: "error",
        message: err instanceof Error ? err.message : "Unknown error",
      });
    }
  }, [apiUrl]);

  useEffect(() => {
    fetchHealth();
    const id = setInterval(fetchHealth, REFRESH_INTERVAL);
    return () => clearInterval(id);
  }, [fetchHealth]);

  return state;
}
```

- [ ] **Step 4: Write StatusDashboard component**

```tsx
// apps/platform/app/status/StatusDashboard.tsx
"use client";
import { useHealthCheck, type FetchState, type HealthResponse } from "./useHealthCheck";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "https://api.real-estate-star.com";

function parseDuration(duration: string): string {
  // "00:00:00.234" → "234ms", "00:00:01.500" → "1500ms"
  const match = duration.match(/(\d+):(\d+):(\d+)\.(\d+)/);
  if (!match) return duration;
  const [, h, m, s, ms] = match;
  const totalMs = (+h * 3600000) + (+m * 60000) + (+s * 1000) + +ms;
  return `${totalMs}ms`;
}

function overallLabel(status: string): string {
  switch (status) {
    case "Healthy": return "All Systems Operational";
    case "Degraded": return "Degraded Performance";
    default: return "Service Disruption";
  }
}

function statusColor(status: string): string {
  switch (status) {
    case "Healthy": return "bg-green-500";
    case "Degraded": return "bg-yellow-500";
    default: return "bg-red-500";
  }
}

export function StatusDashboard() {
  const state = useHealthCheck(API_URL);

  if (state.kind === "loading") {
    return <div data-testid="status-loading">Checking services…</div>;
  }

  if (state.kind === "error") {
    return (
      <div data-testid="status-error" className="text-red-400">
        Unable to reach API
      </div>
    );
  }

  const { data } = state;
  return (
    <div>
      <h2 className="text-2xl font-bold mb-4">{overallLabel(data.status)}</h2>
      <div className="space-y-3">
        {Object.entries(data.entries).map(([name, entry]) => (
          <div
            key={name}
            data-testid={`status-${name}`}
            data-status={entry.status}
            className="flex items-center justify-between p-3 rounded bg-gray-800"
          >
            <div className="flex items-center gap-3">
              <span className={`w-3 h-3 rounded-full ${statusColor(entry.status)}`} />
              <span className="font-medium">{name}</span>
            </div>
            <div className="flex items-center gap-4 text-sm text-gray-400">
              {entry.description && (
                <span className="text-yellow-400">{entry.description}</span>
              )}
              {entry.duration && <span>{parseDuration(entry.duration)}</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Write page.tsx**

```tsx
// apps/platform/app/status/page.tsx
import { StatusDashboard } from "./StatusDashboard";

export const metadata = {
  title: "System Status — Real Estate Star",
  description: "Live status of Real Estate Star services",
};

export default function StatusPage() {
  return (
    <main className="min-h-screen bg-gray-950 text-white px-4 py-16">
      <div className="max-w-2xl mx-auto">
        <h1 className="text-4xl font-bold mb-2">System Status</h1>
        <p className="text-gray-400 mb-8">
          Real-time health of Real Estate Star services
        </p>
        <StatusDashboard />
      </div>
    </main>
  );
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npm run test --prefix apps/platform -- --run status/`
Expected: All 6 tests pass.

- [ ] **Step 7: Write page-level test**

```tsx
// apps/platform/__tests__/status/page.test.tsx
import { render, screen } from "@testing-library/react";
import StatusPage from "@/app/status/page";

beforeEach(() => {
  global.fetch = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ status: "Healthy", entries: {} }),
  });
});

it("renders page heading", () => {
  render(<StatusPage />);
  expect(screen.getByRole("heading", { name: /system status/i })).toBeInTheDocument();
});

it("includes StatusDashboard component", () => {
  render(<StatusPage />);
  expect(screen.getByText("All Systems Operational")).toBeTruthy();
});
```

- [ ] **Step 8: Add status link to platform footer/nav**

Add a "Status" link pointing to `/status` in the platform layout footer.

- [ ] **Step 9: Run full platform test suite with coverage**

Run: `npm run test:coverage --prefix apps/platform`
Expected: 100% branch coverage on new files.

- [ ] **Step 10: Commit**

```bash
git add apps/platform/app/status/ apps/platform/__tests__/status/
git commit -m "feat: add service status page to platform

Real-time health dashboard at /status showing API dependency health.
Auto-refreshes every 30s. Displays per-service response times and
degradation descriptions. 100% branch coverage."
```

---

## Stream L: Dead Code Cleanup + Documentation

### Task 39: Dead code cleanup

Per spec section "Dead Code & Branch Cleanup" — deprecate, clean, and remove dead code introduced or superseded by the lead submission feature.

**Files:**
- Modify: `apps/agent-site/hooks/useCmaSubmit.ts` — add `@deprecated` annotation
- Various files identified by `npx knip` scan

- [ ] **Step 1: Add `@deprecated` to `useCmaSubmit`**

```typescript
/**
 * @deprecated Use server action `submitLead` instead. This hook submitted directly
 * to the API from the browser, which exposes the API URL. Will be removed after
 * all CMA form references migrate to server actions.
 */
export function useCmaSubmit(...) { ... }
```

- [ ] **Step 2: Run lint to clean dead imports**

Run: `npm run lint -- --fix --prefix apps/agent-site`
Run: `npm run lint -- --fix --prefix apps/platform`

- [ ] **Step 3: Run `npx knip` to find unused exports and dead code**

Run: `cd apps/agent-site && npx knip --reporter compact`

Review output. Remove any unused exports, dead variables, or unreferenced files introduced by this feature.

- [ ] **Step 4: Run `npx depcheck` to find unused dependencies**

Run: `cd apps/agent-site && npx depcheck`

- [ ] **Step 5: Create post-merge cleanup checklist**

Document in a PR comment or markdown file:
- [ ] Delete feature branch after merge
- [ ] Remove any feature flags set for gradual rollout
- [ ] Verify no console.log statements in production code
- [ ] Verify no TODO comments left without linked issue

- [ ] **Step 6: Commit**

```bash
git commit -m "chore: deprecate useCmaSubmit, clean dead imports and unused exports

Marks useCmaSubmit as @deprecated in favor of server action submitLead.
Removes dead imports and unused exports identified by knip/depcheck."
```

---

### Task 40: Documentation updates

Per spec section "Documentation Updates" — update all affected documentation files.

**Files:**
- Modify: `CLAUDE.md` — add Leads feature to monorepo structure, add lead-related env vars
- Modify: `.claude/CLAUDE.md` — add Leads to monorepo structure section, add `IFileStorageProvider` to key conventions
- Modify: `docs/onboarding.md` — add lead submission setup steps for new agents
- Modify: `apps/agent-site/README.md` (if exists, else create) — add Turnstile, HMAC, Faro setup docs
- Modify: `apps/api/README.md` (if exists) — add Lead feature endpoints, storage config, health checks
- Modify: `.claude/rules/security-checklists.md` — add HMAC security checklist
- Modify: `.claude/rules/code-quality.md` — add LeadMarkdownRenderer test gates

- [ ] **Step 1: Update CLAUDE.md monorepo structure**

Add `Features/Leads/` to the API section. Document `IFileStorageProvider` abstraction and `Storage:UseLocal` config flag.

- [ ] **Step 2: Update onboarding.md**

Add steps for configuring a new agent's:
- Google Drive folder structure (the 5 numbered folders)
- API key + HMAC secret
- Google Chat webhook URL
- Turnstile site key

- [ ] **Step 3: Update security-checklists.md**

Add "HMAC Security Checklist" triggered when HMAC code is present:
- [ ] Constant-time comparison
- [ ] Timestamp within 5-minute window
- [ ] Body buffering enabled
- [ ] Unique log codes per failure mode
- [ ] HMAC secret not exposed in client code

- [ ] **Step 4: Update code-quality.md**

Add to "Test Quality Gates":
- [ ] Every `LeadMarkdownRenderer.Render*` method has a roundtrip test (render → parse frontmatter → assert values match)
- [ ] Every `IFileStorageProvider` method has both `GDrive` and `Local` implementation tests

- [ ] **Step 5: Commit**

```bash
git commit -m "docs: update documentation for lead submission feature

Update CLAUDE.md, onboarding.md, security checklists, and code quality
gates to reflect new Lead feature, IFileStorageProvider abstraction,
HMAC auth pattern, and Drive folder structure."
```

---

## Execution Order Summary

**Phase 1 — Foundation (all parallel):**
- Task 0: Promote IGwsService
- Tasks 1-3: Storage abstractions (Stream A)
- Tasks 4-7: Domain models + mappers (Stream B)
- Tasks 10-13: Frontend changes (Stream C)
- Tasks 14-15: Security middleware (Stream E)
- Tasks 24-25: Observability (Stream G partial)
- Task 38: Platform status page (Stream K — independent, no API dependency)

**Phase 2 — Feature services (depends on Phase 1):**
- Tasks 8-9, 9b, 16-19: Feature services (Stream D)
- Tasks 26-27: Health checks + Polly (Stream G remainder)

**Phase 3 — Endpoints (depends on Phase 2):**
- Tasks 20-23, 28-29: All endpoints (Stream F)

**Phase 4 — Infrastructure + Monitor (depends on Phase 3):**
- Tasks 30-31: Drive change monitor (Stream H)
- Tasks 32-34: DI registration, CI/CD, OpenAPI (Stream I)

**Phase 5 — Verification + Cleanup (depends on all):**
- Task 35: Legal compliance audit (Stream J)
- Task 36: Integration tests + 100% coverage
- Task 37: Agent config schema update
- Task 39: Dead code cleanup (Stream L)
- Task 40: Documentation updates (Stream L)

**Estimated parallel work streams at peak:** 6 concurrent agents (A, B, C, E, G, K)
