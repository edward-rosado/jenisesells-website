# Platform & Onboarding Chat UI — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the Real Estate Star marketing website with an AI-powered onboarding chat that scrapes agent profiles, deploys their white-label site, demos the CMA pipeline, and collects payment — all in one conversation.

**Architecture:** Next.js 16 frontend (`apps/platform/`) renders a landing page and full-screen chat UI. .NET 10 Minimal API (`apps/api/`) owns the onboarding feature as a vertical slice under `Features/Onboarding/` — state machine, Claude API integration, profile scraping, and session persistence all live inside the feature. Chat streams via SSE. Stripe handles deferred payment.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, .NET 10 Minimal API, Claude API, Stripe, SSE

**Design Doc:** `docs/plans/2026-03-09-platform-onboarding-design.md`

**REPR Conventions (MUST follow):**
- Every endpoint operation is a vertical slice: `Features/Onboarding/{Operation}/{Operation}Endpoint.cs`
- Endpoint classes implement `IEndpoint` (from `Infrastructure/`) — auto-discovered, no manual registration
- HTTP request DTOs live in the operation folder, NOT the same as domain models
- Domain types at feature root: `Features/Onboarding/OnboardingSession.cs`, `OnboardingState.cs`, etc.
- Feature services live inside the feature: `Features/Onboarding/Services/`
- Mappers at feature level: `Features/Onboarding/OnboardingMappers.cs`
- Endpoint class name matches folder name: `CreateSession/` → `CreateSessionEndpoint`
- CancellationToken is always REQUIRED (no `= default`)
- File-scoped namespaces, primary constructors, expression-bodied members

**Target Backend Structure:**
```
Features/Onboarding/
  OnboardingState.cs              # enum (domain type)
  OnboardingSession.cs            # aggregate (domain type)
  ScrapedProfile.cs               # value object (domain type)
  ChatMessage.cs                  # value object (domain type)
  OnboardingMappers.cs            # feature-level mapper
  CreateSession/
    CreateSessionEndpoint.cs      # IEndpoint — POST /onboard
    CreateSessionRequest.cs       # HTTP DTO
    CreateSessionResponse.cs      # HTTP DTO
  GetSession/
    GetSessionEndpoint.cs         # IEndpoint — GET /onboard/{sessionId}
    GetSessionResponse.cs         # HTTP DTO
  PostChat/
    PostChatEndpoint.cs           # IEndpoint — POST /onboard/{sessionId}/chat (SSE)
    PostChatRequest.cs            # HTTP DTO
  Services/
    OnboardingStateMachine.cs     # state transitions + tool gating
    SessionStore.cs               # JSON file persistence (ISessionStore)
    ISessionStore.cs              # interface
    ProfileScraperService.cs      # AI-based profile extraction
    IProfileScraper.cs            # interface
    OnboardingChatService.cs      # Claude API streaming
    SiteDeployService.cs          # config generation + deploy
    StripeService.cs              # SetupIntent + deferred charge
    TrialExpiryService.cs         # background job (IHostedService)
    DomainService.cs              # custom domain DNS validation
  Tools/
    IOnboardingTool.cs            # interface
    ToolDispatcher.cs             # routes tool calls by name
    ScrapeUrlTool.cs
    UpdateProfileTool.cs
    SetBrandingTool.cs
    DeploySiteTool.cs
    SubmitCmaFormTool.cs
    CreateStripeSessionTool.cs
```

---

## Phase 1: Scaffold Platform App

### Task 1: Create Next.js 16 app at `apps/platform/`

**Files:**
- Create: `apps/platform/package.json`
- Create: `apps/platform/tsconfig.json`
- Create: `apps/platform/next.config.ts`
- Create: `apps/platform/tailwind.config.ts` (if needed for v4)
- Create: `apps/platform/postcss.config.mjs`
- Create: `apps/platform/app/layout.tsx`
- Create: `apps/platform/app/page.tsx` (placeholder)
- Create: `apps/platform/vitest.config.ts`
- Create: `apps/platform/vitest.setup.ts`

**Step 1: Scaffold the Next.js app**

```bash
cd apps
npx create-next-app@latest platform \
  --typescript --tailwind --eslint \
  --app --src-dir=false --import-alias="@/*" \
  --use-npm
```

**Step 2: Add test dependencies**

```bash
cd apps/platform
npm install -D vitest @vitejs/plugin-react @vitest/coverage-v8 \
  @testing-library/react @testing-library/jest-dom @testing-library/user-event \
  jsdom
```

**Step 3: Create vitest config**

Create `apps/platform/vitest.config.ts`:

```typescript
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./vitest.setup.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov", "html"],
      include: ["lib/**/*.ts", "components/**/*.tsx", "app/**/*.tsx"],
      exclude: ["**/__tests__/**", "**/*.d.ts"],
      thresholds: {
        branches: 100,
        functions: 100,
        lines: 100,
        statements: 100,
      },
    },
  },
  resolve: {
    alias: { "@": path.resolve(__dirname, ".") },
  },
});
```

Create `apps/platform/vitest.setup.ts`:

```typescript
import "@testing-library/jest-dom/vitest";
```

**Step 4: Add test scripts to package.json**

Add to `scripts`:
```json
{
  "test": "vitest run",
  "test:watch": "vitest",
  "test:coverage": "vitest run --coverage"
}
```

**Step 5: Verify the app builds and starts**

```bash
cd apps/platform
npm run build
npm run dev  # verify http://localhost:3000 loads
```

**Step 6: Commit**

```bash
git add apps/platform/
git commit -m "feat: scaffold platform app (Next.js 16)"
```

---

### Task 2: Update CI pipeline for platform app

**Files:**
- Modify: `.github/workflows/ci.yml`

**Step 1: Add platform job to CI**

Add a new job to `.github/workflows/ci.yml`:

```yaml
  platform:
    name: Platform — lint, test, coverage
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: apps/platform

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: npm
          cache-dependency-path: apps/platform/package-lock.json

      - run: npm ci

      - name: Lint
        run: npm run lint

      - name: Build
        run: npm run build

      - name: Test with coverage
        run: npx vitest run --coverage

      - name: Upload coverage report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: platform-coverage
          path: apps/platform/coverage/
          retention-days: 14
```

**Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add platform app to CI pipeline"
```

---

## Phase 2: Landing Page

### Task 3: Landing page component and route

**Files:**
- Create: `apps/platform/app/page.tsx`
- Create: `apps/platform/__tests__/landing.test.tsx`

**Step 1: Write the failing test**

Create `apps/platform/__tests__/landing.test.tsx`:

```typescript
import { render, screen } from "@testing-library/react";
import LandingPage from "../app/page";

describe("LandingPage", () => {
  it("renders the headline", () => {
    render(<LandingPage />);
    expect(screen.getByText("Stop paying monthly.")).toBeInTheDocument();
  });

  it("renders the price", () => {
    render(<LandingPage />);
    expect(screen.getByText("$900. Everything.")).toBeInTheDocument();
  });

  it("renders the profile URL input", () => {
    render(<LandingPage />);
    expect(
      screen.getByPlaceholderText(/Paste your Zillow or Realtor\.com URL/i)
    ).toBeInTheDocument();
  });

  it("renders the CTA button", () => {
    render(<LandingPage />);
    expect(
      screen.getByRole("button", { name: /Get Started Free/i })
    ).toBeInTheDocument();
  });

  it("renders the trial disclaimer", () => {
    render(<LandingPage />);
    expect(
      screen.getByText(/7-day free trial\. No credit card\./i)
    ).toBeInTheDocument();
  });
});
```

**Step 2: Run test to verify it fails**

```bash
cd apps/platform && npx vitest run __tests__/landing.test.tsx
```

Expected: FAIL — `LandingPage` is default Next.js page.

**Step 3: Implement the landing page**

Replace `apps/platform/app/page.tsx`:

```typescript
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export default function LandingPage() {
  const [profileUrl, setProfileUrl] = useState("");
  const router = useRouter();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const params = profileUrl
      ? `?profileUrl=${encodeURIComponent(profileUrl)}`
      : "";
    router.push(`/onboard${params}`);
  }

  return (
    <main className="min-h-screen bg-gray-950 text-white flex flex-col items-center justify-center px-4">
      <div className="max-w-xl w-full text-center">
        <h1 className="text-5xl md:text-6xl font-bold tracking-tight mb-2">
          Stop paying monthly.
        </h1>
        <p className="text-4xl md:text-5xl font-bold text-emerald-400 mb-8">
          $900. Everything.
        </p>
        <p className="text-lg text-gray-400 mb-12">
          Website. CMA automation. Lead management. One payment. Done.
        </p>
        <form onSubmit={handleSubmit} className="space-y-4">
          <input
            type="text"
            value={profileUrl}
            onChange={(e) => setProfileUrl(e.target.value)}
            placeholder="Paste your Zillow or Realtor.com URL"
            className="w-full px-4 py-3 rounded-lg bg-gray-800 border border-gray-700 text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
          <button
            type="submit"
            className="w-full px-6 py-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-lg transition-colors"
          >
            Get Started Free
          </button>
        </form>
        <p className="text-sm text-gray-500 mt-4">
          7-day free trial. No credit card.
        </p>
      </div>
    </main>
  );
}
```

Note: The `Log In` link lives in `layout.tsx` header (Task 4).

**Step 4: Run test to verify it passes**

```bash
cd apps/platform && npx vitest run __tests__/landing.test.tsx
```

**Step 5: Commit**

```bash
git add apps/platform/app/page.tsx apps/platform/__tests__/landing.test.tsx
git commit -m "feat: add landing page with dark theme and CTA"
```

---

### Task 4: Layout with header and branding

**Files:**
- Modify: `apps/platform/app/layout.tsx`
- Create: `apps/platform/__tests__/layout.test.tsx`

**Step 1: Write the failing test**

Create `apps/platform/__tests__/layout.test.tsx`:

```typescript
import { render, screen } from "@testing-library/react";
import RootLayout from "../app/layout";

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("RootLayout", () => {
  it("renders the brand name", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByText("Real Estate Star")).toBeInTheDocument();
  });

  it("renders the Log In link", () => {
    render(
      <RootLayout>
        <div>child</div>
      </RootLayout>
    );
    expect(screen.getByRole("link", { name: /Log In/i })).toBeInTheDocument();
  });

  it("renders children", () => {
    render(
      <RootLayout>
        <div>test child</div>
      </RootLayout>
    );
    expect(screen.getByText("test child")).toBeInTheDocument();
  });
});
```

**Step 2: Implement the layout**

Modify `apps/platform/app/layout.tsx`:

```typescript
import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "Real Estate Star",
  description: "Stop paying monthly. $900. Everything.",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className="bg-gray-950 text-white antialiased">
        <header className="fixed top-0 w-full z-50 flex items-center justify-between px-6 py-4">
          <span className="text-lg font-bold tracking-tight">
            ★ Real Estate Star
          </span>
          <Link
            href="/login"
            className="text-sm text-gray-400 hover:text-white transition-colors"
          >
            Log In
          </Link>
        </header>
        {children}
      </body>
    </html>
  );
}
```

**Step 3: Run tests, commit**

```bash
cd apps/platform && npx vitest run __tests__/layout.test.tsx
git add apps/platform/app/layout.tsx apps/platform/__tests__/
git commit -m "feat: add layout with brand header and Log In link"
```

---

## Phase 3: Backend Onboarding Domain & State Machine

### Task 5: Onboarding domain types (state enum, session, profile, message)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingState.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/ScrapedProfile.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/ChatMessage.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingSession.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/OnboardingSessionTests.cs`

**Step 1: Write the failing test**

Create `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/OnboardingSessionTests.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding;

public class OnboardingSessionTests
{
    [Fact]
    public void NewSession_StartsInScrapeProfileState()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test-agent");
        Assert.Equal(OnboardingState.ScrapeProfile, session.CurrentState);
    }

    [Fact]
    public void NewSession_HasUniqueId()
    {
        var s1 = OnboardingSession.Create("https://zillow.com/profile/a");
        var s2 = OnboardingSession.Create("https://zillow.com/profile/b");
        Assert.NotEqual(s1.Id, s2.Id);
    }

    [Fact]
    public void NewSession_StoresProfileUrl()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        Assert.Equal("https://zillow.com/profile/test", session.ProfileUrl);
    }

    [Fact]
    public void NewSession_WithoutUrl_StartsInScrapeProfileState()
    {
        var session = OnboardingSession.Create(null);
        Assert.Equal(OnboardingState.ScrapeProfile, session.CurrentState);
        Assert.Null(session.ProfileUrl);
    }

    [Fact]
    public void NewSession_HasEmptyMessageHistory()
    {
        var session = OnboardingSession.Create(null);
        Assert.Empty(session.Messages);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "OnboardingSessionTests"
```

Expected: FAIL — types don't exist.

**Step 3: Create the domain types**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingState.cs`:

```csharp
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.Onboarding;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingState
{
    ScrapeProfile,
    ConfirmIdentity,
    CollectBranding,
    GenerateSite,
    PreviewSite,
    DemoCma,
    ShowResults,
    CollectPayment,
    TrialActivated
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/ScrapedProfile.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding;

public sealed record ScrapedProfile
{
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Brokerage { get; init; }
    public string? LicenseId { get; init; }
    public string? State { get; init; }
    public string? OfficeAddress { get; init; }
    public string[]? ServiceAreas { get; init; }
    public string? Bio { get; init; }
    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? LogoUrl { get; init; }
    public int? YearsExperience { get; init; }
    public int? HomesSold { get; init; }
    public double? AvgRating { get; init; }
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/ChatMessage.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding;

public sealed record ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingSession.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding;

public sealed class OnboardingSession
{
    public required string Id { get; init; }
    public OnboardingState CurrentState { get; set; } = OnboardingState.ScrapeProfile;
    public string? ProfileUrl { get; init; }
    public ScrapedProfile? Profile { get; set; }
    public List<ChatMessage> Messages { get; init; } = [];
    public string? AgentConfigId { get; set; }
    public string? StripeSetupIntentId { get; set; }
    public string? SiteUrl { get; set; }
    public string? CustomDomain { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static OnboardingSession Create(string? profileUrl) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..12],
        ProfileUrl = profileUrl
    };
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "OnboardingSessionTests"
```

**Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Api/Features/Onboarding/ apps/api/RealEstateStar.Api.Tests/Features/Onboarding/
git commit -m "feat: add onboarding domain types (state, session, profile, message)"
```

---

### Task 6: State machine with transitions and tool access

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingStateMachine.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/StateMachineTests.cs`

**Step 1: Write the failing test**

Create `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/StateMachineTests.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class StateMachineTests
{
    private readonly OnboardingStateMachine _sm = new();

    [Fact]
    public void CanAdvance_FromScrapeProfile_ToConfirmIdentity()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        Assert.True(_sm.CanAdvance(session, OnboardingState.ConfirmIdentity));
    }

    [Fact]
    public void CannotSkip_FromScrapeProfile_ToGenerateSite()
    {
        var session = OnboardingSession.Create(null);
        Assert.False(_sm.CanAdvance(session, OnboardingState.GenerateSite));
    }

    [Fact]
    public void Advance_UpdatesCurrentState()
    {
        var session = OnboardingSession.Create(null);
        _sm.Advance(session, OnboardingState.ConfirmIdentity);
        Assert.Equal(OnboardingState.ConfirmIdentity, session.CurrentState);
    }

    [Fact]
    public void Advance_ToInvalidState_Throws()
    {
        var session = OnboardingSession.Create(null);
        Assert.Throws<InvalidOperationException>(
            () => _sm.Advance(session, OnboardingState.CollectPayment));
    }

    [Fact]
    public void GetAllowedTools_ScrapeProfile_ReturnsScrapeTools()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.ScrapeProfile);
        Assert.Contains("scrape_url", tools);
        Assert.DoesNotContain("deploy_site", tools);
    }

    [Fact]
    public void GetAllowedTools_CollectPayment_ReturnsStripeTools()
    {
        var tools = _sm.GetAllowedTools(OnboardingState.CollectPayment);
        Assert.Contains("create_stripe_session", tools);
        Assert.DoesNotContain("scrape_url", tools);
    }

    [Theory]
    [InlineData(OnboardingState.ScrapeProfile, OnboardingState.ConfirmIdentity)]
    [InlineData(OnboardingState.ConfirmIdentity, OnboardingState.CollectBranding)]
    [InlineData(OnboardingState.CollectBranding, OnboardingState.GenerateSite)]
    [InlineData(OnboardingState.GenerateSite, OnboardingState.PreviewSite)]
    [InlineData(OnboardingState.PreviewSite, OnboardingState.DemoCma)]
    [InlineData(OnboardingState.DemoCma, OnboardingState.ShowResults)]
    [InlineData(OnboardingState.ShowResults, OnboardingState.CollectPayment)]
    [InlineData(OnboardingState.CollectPayment, OnboardingState.TrialActivated)]
    public void AllTransitions_AreValid(OnboardingState from, OnboardingState to)
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = from;
        Assert.True(_sm.CanAdvance(session, to));
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "StateMachineTests"
```

**Step 3: Implement the state machine**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingStateMachine.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.Services;

public class OnboardingStateMachine
{
    private static readonly Dictionary<OnboardingState, OnboardingState[]> Transitions = new()
    {
        [OnboardingState.ScrapeProfile] = [OnboardingState.ConfirmIdentity],
        [OnboardingState.ConfirmIdentity] = [OnboardingState.CollectBranding],
        [OnboardingState.CollectBranding] = [OnboardingState.GenerateSite],
        [OnboardingState.GenerateSite] = [OnboardingState.PreviewSite],
        [OnboardingState.PreviewSite] = [OnboardingState.DemoCma],
        [OnboardingState.DemoCma] = [OnboardingState.ShowResults],
        [OnboardingState.ShowResults] = [OnboardingState.CollectPayment],
        [OnboardingState.CollectPayment] = [OnboardingState.TrialActivated],
        [OnboardingState.TrialActivated] = [],
    };

    private static readonly Dictionary<OnboardingState, string[]> ToolsByState = new()
    {
        [OnboardingState.ScrapeProfile] = ["scrape_url", "update_profile"],
        [OnboardingState.ConfirmIdentity] = ["update_profile"],
        [OnboardingState.CollectBranding] = ["extract_colors", "set_branding"],
        [OnboardingState.GenerateSite] = ["deploy_site"],
        [OnboardingState.PreviewSite] = ["get_preview_url"],
        [OnboardingState.DemoCma] = ["submit_cma_form"],
        [OnboardingState.ShowResults] = [],
        [OnboardingState.CollectPayment] = ["create_stripe_session"],
        [OnboardingState.TrialActivated] = [],
    };

    public bool CanAdvance(OnboardingSession session, OnboardingState targetState)
        => Transitions.TryGetValue(session.CurrentState, out var allowed)
           && allowed.Contains(targetState);

    public void Advance(OnboardingSession session, OnboardingState targetState)
    {
        if (!CanAdvance(session, targetState))
            throw new InvalidOperationException(
                $"Cannot transition from {session.CurrentState} to {targetState}");

        session.CurrentState = targetState;
        session.UpdatedAt = DateTime.UtcNow;
    }

    public string[] GetAllowedTools(OnboardingState state)
        => ToolsByState.GetValueOrDefault(state, []);
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "StateMachineTests"
```

**Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Api/Features/Onboarding/Services/ \
  apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/
git commit -m "feat: add onboarding state machine with transitions and tool access"
```

---

### Task 7: Session persistence service (JSON files)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/ISessionStore.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/JsonFileSessionStore.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/SessionStoreTests.cs`

**Step 1: Write the failing test**

Create `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/SessionStoreTests.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class SessionStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly JsonFileSessionStore _store;

    public SessionStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-sessions-{Guid.NewGuid():N}");
        _store = new JsonFileSessionStore(_testDir);
    }

    public void Dispose() => Directory.Delete(_testDir, true);

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded!.Id);
        Assert.Equal(session.ProfileUrl, loaded.ProfileUrl);
    }

    [Fact]
    public async Task Load_NonExistentId_ReturnsNull()
    {
        var result = await _store.LoadAsync("does-not-exist", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_PreservesMessages()
    {
        var session = OnboardingSession.Create(null);
        session.Messages.Add(new ChatMessage { Role = "user", Content = "hello" });
        session.Messages.Add(new ChatMessage { Role = "assistant", Content = "hi" });
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.Equal(2, loaded!.Messages.Count);
        Assert.Equal("hello", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task Save_PreservesStateChanges()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.Equal(OnboardingState.CollectBranding, loaded!.CurrentState);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "SessionStoreTests"
```

**Step 3: Implement the session store**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/Services/ISessionStore.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface ISessionStore
{
    Task SaveAsync(OnboardingSession session, CancellationToken ct);
    Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct);
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/Services/JsonFileSessionStore.cs`:

```csharp
using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class JsonFileSessionStore(string basePath) : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonFileSessionStore()
        : this(Path.Combine(AppContext.BaseDirectory, "data", "sessions")) { }

    public async Task SaveAsync(OnboardingSession session, CancellationToken ct)
    {
        Directory.CreateDirectory(basePath);
        var path = GetPath(session.Id);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public async Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<OnboardingSession>(json, JsonOptions);
    }

    private string GetPath(string sessionId)
        => Path.Combine(basePath, $"{sessionId}.json");
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "SessionStoreTests"
```

**Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Api/Features/Onboarding/Services/ \
  apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/
git commit -m "feat: add JSON file-based session persistence"
```

---

## Phase 4: Onboarding API Endpoints (Vertical Slices)

### Task 8: CreateSession endpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/CreateSessionRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/CreateSessionResponse.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/CreateSessionEndpoint.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingMappers.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/CreateSession/CreateSessionEndpointTests.cs`

**Step 1: Write the failing test**

Create `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/CreateSession/CreateSessionEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.CreateSession;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.CreateSession;

public class CreateSessionEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    [Fact]
    public async Task Handle_WithProfileUrl_CreatesSession()
    {
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { ProfileUrl = "https://zillow.com/profile/test" };
        var result = await CreateSessionEndpoint.Handle(request, _mockStore.Object, CancellationToken.None);

        var ok = Assert.IsType<Ok<CreateSessionResponse>>(result);
        Assert.False(string.IsNullOrEmpty(ok.Value!.SessionId));
        _mockStore.Verify(s => s.SaveAsync(
            It.Is<OnboardingSession>(sess => sess.ProfileUrl == "https://zillow.com/profile/test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutProfileUrl_CreatesSession()
    {
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateSessionRequest { ProfileUrl = null };
        var result = await CreateSessionEndpoint.Handle(request, _mockStore.Object, CancellationToken.None);

        var ok = Assert.IsType<Ok<CreateSessionResponse>>(result);
        Assert.False(string.IsNullOrEmpty(ok.Value!.SessionId));
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "CreateSessionEndpointTests"
```

**Step 3: Implement**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/CreateSessionRequest.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.CreateSession;

public sealed record CreateSessionRequest
{
    public string? ProfileUrl { get; init; }
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/CreateSessionResponse.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.CreateSession;

public sealed record CreateSessionResponse(string SessionId);
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingMappers.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding.CreateSession;
using RealEstateStar.Api.Features.Onboarding.GetSession;

namespace RealEstateStar.Api.Features.Onboarding;

public static class OnboardingMappers
{
    public static OnboardingSession ToSession(this CreateSessionRequest request)
        => OnboardingSession.Create(request.ProfileUrl);

    public static CreateSessionResponse ToCreateResponse(this OnboardingSession session)
        => new(session.Id);

    public static GetSessionResponse ToGetResponse(this OnboardingSession session)
        => new()
        {
            SessionId = session.Id,
            CurrentState = session.CurrentState,
            ProfileUrl = session.ProfileUrl,
            Profile = session.Profile,
            Messages = session.Messages,
            SiteUrl = session.SiteUrl,
            CreatedAt = session.CreatedAt,
        };
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/CreateSessionEndpoint.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.CreateSession;

public class CreateSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/onboard", Handle);
    }

    internal static async Task<IResult> Handle(
        CreateSessionRequest request,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = request.ToSession();
        await sessionStore.SaveAsync(session, ct);
        return Results.Ok(session.ToCreateResponse());
    }
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "CreateSessionEndpointTests"
```

**Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Api/Features/Onboarding/CreateSession/ \
  apps/api/RealEstateStar.Api/Features/Onboarding/OnboardingMappers.cs \
  apps/api/RealEstateStar.Api.Tests/Features/Onboarding/CreateSession/
git commit -m "feat: add CreateSession endpoint (POST /onboard)"
```

---

### Task 9: GetSession endpoint

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/GetSession/GetSessionResponse.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/GetSession/GetSessionEndpoint.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/GetSession/GetSessionEndpointTests.cs`

**Step 1: Write the failing test**

Create `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/GetSession/GetSessionEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.GetSession;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.GetSession;

public class GetSessionEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    [Fact]
    public async Task Handle_ValidId_ReturnsSession()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await GetSessionEndpoint.Handle(session.Id, _mockStore.Object, CancellationToken.None);

        var ok = Assert.IsType<Ok<GetSessionResponse>>(result);
        Assert.Equal(session.Id, ok.Value!.SessionId);
        Assert.Equal(OnboardingState.ScrapeProfile, ok.Value.CurrentState);
    }

    [Fact]
    public async Task Handle_InvalidId_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var result = await GetSessionEndpoint.Handle("nope", _mockStore.Object, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "GetSessionEndpointTests"
```

**Step 3: Implement**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/GetSession/GetSessionResponse.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.GetSession;

public sealed record GetSessionResponse
{
    public required string SessionId { get; init; }
    public required OnboardingState CurrentState { get; init; }
    public string? ProfileUrl { get; init; }
    public ScrapedProfile? Profile { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public string? SiteUrl { get; init; }
    public required DateTime CreatedAt { get; init; }
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/GetSession/GetSessionEndpoint.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.GetSession;

public class GetSessionEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/onboard/{sessionId}", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        return session is null
            ? Results.NotFound()
            : Results.Ok(session.ToGetResponse());
    }
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "GetSessionEndpointTests"
```

**Step 5: Commit**

```bash
git add apps/api/RealEstateStar.Api/Features/Onboarding/GetSession/ \
  apps/api/RealEstateStar.Api.Tests/Features/Onboarding/GetSession/
git commit -m "feat: add GetSession endpoint (GET /onboard/{sessionId})"
```

---

### Task 10: PostChat endpoint with SSE streaming stub

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/PostChat/PostChatRequest.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/PostChat/PostChatEndpoint.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/PostChat/PostChatEndpointTests.cs`

**Step 1: Write the failing test**

Create `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/PostChat/PostChatEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Moq;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.PostChat;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.PostChat;

public class PostChatEndpointTests
{
    private readonly Mock<ISessionStore> _mockStore = new();

    [Fact]
    public async Task Handle_InvalidSession_Returns404()
    {
        _mockStore.Setup(s => s.LoadAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnboardingSession?)null);

        var request = new PostChatRequest { Message = "hello" };
        var result = await PostChatEndpoint.Handle(
            "nope", request, _mockStore.Object, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task Handle_ValidSession_AddsMessageAndSaves()
    {
        var session = OnboardingSession.Create(null);
        _mockStore.Setup(s => s.LoadAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockStore.Setup(s => s.SaveAsync(It.IsAny<OnboardingSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new PostChatRequest { Message = "hello" };
        var result = await PostChatEndpoint.Handle(
            session.Id, request, _mockStore.Object, CancellationToken.None);

        Assert.Equal(1, session.Messages.Count);
        Assert.Equal("user", session.Messages[0].Role);
        Assert.Equal("hello", session.Messages[0].Content);
        _mockStore.Verify(s => s.SaveAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "PostChatEndpointTests"
```

**Step 3: Implement**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/PostChat/PostChatRequest.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.PostChat;

public sealed record PostChatRequest
{
    public required string Message { get; init; }
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/PostChat/PostChatEndpoint.cs`:

```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.Onboarding.PostChat;

// No response DTO — this endpoint streams SSE. The stub returns JSON for now;
// Task 18 (Claude chat service) replaces this with real SSE streaming.
public class PostChatEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapPost("/onboard/{sessionId}/chat", Handle);
    }

    internal static async Task<IResult> Handle(
        string sessionId,
        PostChatRequest request,
        ISessionStore sessionStore,
        CancellationToken ct)
    {
        var session = await sessionStore.LoadAsync(sessionId, ct);
        if (session is null) return Results.NotFound();

        session.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = request.Message,
        });

        // TODO: Wire OnboardingChatService here (Task 18).
        // For now, echo back a stub assistant message.
        session.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = $"[Stub] Received: {request.Message}",
        });

        await sessionStore.SaveAsync(session, ct);

        return Results.Ok(new { response = session.Messages[^1].Content });
    }
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "PostChatEndpointTests"
```

**Step 5: Register ISessionStore in DI**

Add to `apps/api/RealEstateStar.Api/Program.cs`:

```csharp
builder.Services.AddSingleton<ISessionStore, JsonFileSessionStore>();
```

Use the fully qualified import:
```csharp
using RealEstateStar.Api.Features.Onboarding.Services;
```

**Step 6: Commit**

```bash
git add apps/api/RealEstateStar.Api/Features/Onboarding/PostChat/ \
  apps/api/RealEstateStar.Api.Tests/Features/Onboarding/PostChat/ \
  apps/api/RealEstateStar.Api/Program.cs
git commit -m "feat: add PostChat endpoint with SSE stub (POST /onboard/{sessionId}/chat)"
```

---

## Phase 5: Chat UI Frontend

### Task 11: Chat window and message bubble components

**Files:**
- Create: `apps/platform/components/chat/ChatWindow.tsx`
- Create: `apps/platform/components/chat/MessageBubble.tsx`
- Create: `apps/platform/__tests__/components/ChatWindow.test.tsx`
- Create: `apps/platform/__tests__/components/MessageBubble.test.tsx`

**Step 1: Write the failing test**

Create `apps/platform/__tests__/components/MessageBubble.test.tsx`:

```typescript
import { render, screen } from "@testing-library/react";
import { MessageBubble } from "../../components/chat/MessageBubble";

describe("MessageBubble", () => {
  it("renders user message with right alignment", () => {
    render(<MessageBubble role="user" content="Hello!" />);
    expect(screen.getByText("Hello!")).toBeInTheDocument();
  });

  it("renders assistant message with left alignment", () => {
    render(<MessageBubble role="assistant" content="Hi there!" />);
    expect(screen.getByText("Hi there!")).toBeInTheDocument();
  });
});
```

Create `apps/platform/__tests__/components/ChatWindow.test.tsx`:

```typescript
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatWindow } from "../../components/chat/ChatWindow";

describe("ChatWindow", () => {
  it("renders input field", () => {
    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);
    expect(screen.getByPlaceholderText(/Type a message/i)).toBeInTheDocument();
  });

  it("renders send button", () => {
    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);
    expect(screen.getByRole("button", { name: /Send/i })).toBeInTheDocument();
  });

  it("renders initial messages", () => {
    render(
      <ChatWindow
        sessionId="test-123"
        initialMessages={[
          { role: "assistant", content: "Welcome!" },
        ]}
      />
    );
    expect(screen.getByText("Welcome!")).toBeInTheDocument();
  });
});
```

**Step 2-5: Implement, test, commit** — follow TDD pattern.

Component sends POST to `/onboard/{sessionId}/chat` with the message, renders response.

**Commit message:** `feat: add chat window and message bubble components`

---

### Task 12: Onboard page with session creation

**Files:**
- Create: `apps/platform/app/onboard/page.tsx`
- Create: `apps/platform/__tests__/onboard.test.tsx`

**Step 1: Write the failing test**

Create `apps/platform/__tests__/onboard.test.tsx`:

```typescript
import { render, screen, waitFor } from "@testing-library/react";
import OnboardPage from "../app/onboard/page";

// Mock fetch
global.fetch = vi.fn().mockResolvedValue({
  ok: true,
  json: () => Promise.resolve({ sessionId: "abc123" }),
});

// Mock useSearchParams
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("profileUrl=https://zillow.com/profile/test"),
}));

describe("OnboardPage", () => {
  it("creates a session on mount", async () => {
    render(<OnboardPage />);
    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/onboard"),
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  it("shows loading state initially", () => {
    render(<OnboardPage />);
    expect(screen.getByText(/Starting your onboarding/i)).toBeInTheDocument();
  });
});
```

**Step 2: Implement**

Create `apps/platform/app/onboard/page.tsx`:

```typescript
"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { ChatWindow } from "@/components/chat/ChatWindow";

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export default function OnboardPage() {
  const searchParams = useSearchParams();
  const profileUrl = searchParams.get("profileUrl");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function createSession() {
      try {
        const res = await fetch(`${API_BASE}/onboard`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ profileUrl }),
        });
        if (!res.ok) throw new Error("Failed to create session");
        const data = await res.json();
        setSessionId(data.sessionId);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Something went wrong");
      }
    }
    createSession();
  }, [profileUrl]);

  if (error) {
    return (
      <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
        <p className="text-red-400">{error}</p>
      </main>
    );
  }

  if (!sessionId) {
    return (
      <main className="min-h-screen bg-gray-950 text-white flex items-center justify-center">
        <div className="flex items-center gap-3">
          <div className="h-5 w-5 rounded-full border-2 border-emerald-500 border-t-transparent animate-spin" />
          <span className="text-gray-400">Starting your onboarding...</span>
        </div>
      </main>
    );
  }

  return <ChatWindow sessionId={sessionId} initialMessages={[]} />;
}
```

**Step 3: Run tests, commit**

```bash
cd apps/platform && npx vitest run __tests__/onboard.test.tsx
git add apps/platform/app/onboard/ apps/platform/__tests__/onboard.test.tsx
git commit -m "feat: add onboard page with session creation and chat rendering"
```

---

## Phase 6: Rich Chat Cards

### Task 13: Profile card component

**Files:**
- Create: `apps/platform/components/chat/ProfileCard.tsx`
- Create: `apps/platform/__tests__/components/ProfileCard.test.tsx`

Renders a card with photo, name, brokerage, state, stats, and a "Looks right" action button. Calls `onConfirm` prop when button clicked.

**Commit message:** `feat: add profile card chat component`

---

### Task 14: Color palette component

**Files:**
- Create: `apps/platform/components/chat/ColorPalette.tsx`
- Create: `apps/platform/__tests__/components/ColorPalette.test.tsx`

Renders extracted branding colors as swatches with labels. Includes "Customize" button that allows editing. Calls `onConfirm` with final color selections.

**Commit message:** `feat: add color palette chat component`

---

### Task 15: Site preview component

**Files:**
- Create: `apps/platform/components/chat/SitePreview.tsx`
- Create: `apps/platform/__tests__/components/SitePreview.test.tsx`

Renders a responsive iframe showing the deployed agent site. Includes "Approve" button.

**Commit message:** `feat: add site preview iframe chat component`

---

### Task 16: Feature checklist component

**Files:**
- Create: `apps/platform/components/chat/FeatureChecklist.tsx`
- Create: `apps/platform/__tests__/components/FeatureChecklist.test.tsx`

Renders the platform capabilities list with checkmarks — the "wow expansion" moment showing what Real Estate Star automates beyond the CMA demo.

**Commit message:** `feat: add feature checklist chat component`

---

### Task 17: Payment card component (Stripe stub)

**Files:**
- Create: `apps/platform/components/chat/PaymentCard.tsx`
- Create: `apps/platform/__tests__/components/PaymentCard.test.tsx`

Renders a styled card with "$900 — One Time" and a "Start Free Trial" button. Stripe Elements wired in Task 24. The button calls `onPaymentComplete` prop.

**Commit message:** `feat: add payment card chat component (Stripe stub)`

---

### Task 18: Message renderer that dispatches to card components

**Files:**
- Create: `apps/platform/components/chat/MessageRenderer.tsx`
- Create: `apps/platform/__tests__/components/MessageRenderer.test.tsx`
- Modify: `apps/platform/components/chat/ChatWindow.tsx`

The `MessageRenderer` takes a `ChatMessage` and renders the appropriate component based on `type`: text → `MessageBubble`, profile_card → `ProfileCard`, color_palette → `ColorPalette`, etc.

Update `ChatWindow` to use `MessageRenderer` instead of `MessageBubble` directly.

**Commit message:** `feat: add message renderer that dispatches to chat card components`

---

## Phase 7: Profile Scraping

### Task 19: Profile scraper service (AI-based)

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/IProfileScraper.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/ProfileScraperService.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/ProfileScraperTests.cs`

**Step 1: Write the failing test**

Test with a mock HTTP client and mock Claude response. Verify:
- Returns `ScrapedProfile` with extracted fields
- Handles fetch failures gracefully (returns partial profile)
- Handles non-real-estate pages (returns null)

**Step 2: Implement**

Create `apps/api/RealEstateStar.Api/Features/Onboarding/Services/IProfileScraper.cs`:

```csharp
namespace RealEstateStar.Api.Features.Onboarding.Services;

public interface IProfileScraper
{
    Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct);
}
```

Create `apps/api/RealEstateStar.Api/Features/Onboarding/Services/ProfileScraperService.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class ProfileScraperService(
    HttpClient httpClient,
    ILogger<ProfileScraperService> logger) : IProfileScraper
{
    public async Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct)
    {
        // 1. Fetch page HTML
        // 2. Strip scripts/styles, extract text content
        // 3. Send to Claude API with extraction prompt
        // 4. Parse structured response into ScrapedProfile
        throw new NotImplementedException();
    }
}
```

**NuGet dependency:** Add Anthropic SDK package:

```bash
cd apps/api && dotnet add RealEstateStar.Api/RealEstateStar.Api.csproj package Anthropic
```

**Step 3: Run tests, commit**

```bash
cd apps/api && dotnet test --filter "ProfileScraperTests"
git add apps/api/RealEstateStar.Api/Features/Onboarding/Services/ \
  apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/ \
  apps/api/RealEstateStar.Api/RealEstateStar.Api.csproj
git commit -m "feat: add AI-based profile scraper service"
```

---

## Phase 8: Claude Chat Service

### Task 20: Claude API chat service with state-aware tools

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/OnboardingChatService.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/ChatServiceTests.cs`
- Modify: `apps/api/RealEstateStar.Api/Features/Onboarding/PostChat/PostChatEndpoint.cs`

**Step 1: Write the failing test**

Test that:
- Service builds Claude messages from session history
- Only includes tools allowed by current state
- System prompt includes onboarding context and agent profile
- Streams response chunks

**Step 2: Implement**

```csharp
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class OnboardingChatService(
    OnboardingStateMachine stateMachine,
    ILogger<OnboardingChatService> logger)
{
    public async IAsyncEnumerable<string> StreamResponseAsync(
        OnboardingSession session,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var tools = stateMachine.GetAllowedTools(session.CurrentState);
        var systemPrompt = BuildSystemPrompt(session);
        // Call Claude API with streaming, yield chunks
        yield return "[Stub] Streaming not yet implemented";
    }

    private static string BuildSystemPrompt(OnboardingSession session)
    {
        // Include current state, profile data, instructions per state
        return $"You are an onboarding assistant. Current state: {session.CurrentState}";
    }
}
```

**Step 3: Wire into PostChatEndpoint**

Replace the TODO stub in `PostChatEndpoint.Handle` with calls to `OnboardingChatService.StreamResponseAsync`. Set response content type to `text/event-stream` for SSE.

**Commit message:** `feat: add Claude API chat service with state-aware tool access`

---

### Task 21: Tool execution handlers

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/IOnboardingTool.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/ToolDispatcher.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/ScrapeUrlTool.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/UpdateProfileTool.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/SetBrandingTool.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/DeploySiteTool.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/ToolDispatcherTests.cs`

**Step 1: Write the failing test**

Test the dispatcher routes tool calls to correct handlers and returns results.

**Step 2: Implement**

```csharp
using System.Text.Json;

namespace RealEstateStar.Api.Features.Onboarding.Tools;

public interface IOnboardingTool
{
    string Name { get; }
    Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct);
}

public class ToolDispatcher(IEnumerable<IOnboardingTool> tools)
{
    private readonly Dictionary<string, IOnboardingTool> _tools =
        tools.ToDictionary(t => t.Name);

    public async Task<string> DispatchAsync(
        string toolName,
        JsonElement parameters,
        OnboardingSession session,
        CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            throw new InvalidOperationException($"Unknown tool: {toolName}");

        return await tool.ExecuteAsync(parameters, session, ct);
    }
}
```

Each tool implementation receives parameters from Claude's `tool_use` response, executes the action, and returns a result string.

**Commit message:** `feat: add onboarding tool execution handlers`

---

## Phase 9: Site Deploy Integration

### Task 22: Site deploy service — generate config and deploy

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/SiteDeployService.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/SiteDeployTests.cs`

This service:
1. Generates `{agent-id}.json` config from `ScrapedProfile` + branding choices
2. Generates `{agent-id}.content.json` with default content sections
3. Writes config files to `config/agents/`
4. Returns the preview URL (`{agent-id}.realestatestar.com`)

For v1, writes config files locally and triggers a build. Full PR pipeline can be wired later.

**Commit message:** `feat: add site deploy service for onboarding`

---

## Phase 10: CMA Demo Integration

### Task 23: CMA demo trigger in onboarding

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/SubmitCmaFormTool.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Tools/CmaToolTests.cs`

This tool:
1. Pre-fills a CMA form submission with a sample property address
2. Submits it to the existing CMA pipeline endpoint
3. Returns status updates as the pipeline runs
4. Tells the agent to check their inbox and Drive

Depends on the CMA pipeline being functional.

**Commit message:** `feat: add CMA demo trigger tool for onboarding`

---

## Phase 11: Stripe Integration

### Task 24: Stripe SetupIntent service

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/StripeService.cs`
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Tools/CreateStripeSessionTool.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/StripeServiceTests.cs`
- Modify: `apps/platform/components/chat/PaymentCard.tsx`

**Step 1: Add Stripe NuGet package**

```bash
cd apps/api && dotnet add RealEstateStar.Api/RealEstateStar.Api.csproj package Stripe.net
```

**Step 2: Add Stripe.js to frontend**

```bash
cd apps/platform && npm install @stripe/stripe-js @stripe/react-stripe-js
```

**Step 3: Implement StripeService**

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.Onboarding.Services;

public class StripeService(
    IConfiguration config,
    ILogger<StripeService> logger)
{
    public async Task<string> CreateSetupIntentAsync(string sessionId, CancellationToken ct)
    {
        // Create Stripe SetupIntent, return client secret for frontend
        throw new NotImplementedException();
    }

    public async Task ChargeAsync(string setupIntentId, CancellationToken ct)
    {
        // Called by scheduled job on day 7
        // Creates PaymentIntent for $900 using saved payment method
        throw new NotImplementedException();
    }
}
```

**Step 4: Update PaymentCard to use Stripe Elements**

Replace the stub button with `@stripe/react-stripe-js` `Elements` + `PaymentElement`.

**Commit message:** `feat: add Stripe SetupIntent integration for deferred payment`

---

### Task 25: Trial expiry scheduled job

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/TrialExpiryService.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/TrialExpiryTests.cs`

A background service that runs daily using `BackgroundService` / `IHostedService`:
1. Scans sessions in `TrialActivated` state
2. If trial started >= 7 days ago and not cancelled → charge via StripeService
3. If cancelled → deactivate site, clean up

```csharp
namespace RealEstateStar.Api.Features.Onboarding.Services;

public class TrialExpiryService(
    ISessionStore sessionStore,
    StripeService stripeService,
    ILogger<TrialExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run daily, check trial expirations
    }
}
```

**Commit message:** `feat: add trial expiry background service`

---

## Phase 12: Custom Domain Support

### Task 26: Custom domain configuration

**Files:**
- Create: `apps/api/RealEstateStar.Api/Features/Onboarding/Services/DomainService.cs`
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/Services/DomainServiceTests.cs`

During the `TrialActivated` state, the AI asks if the agent has a custom domain. If yes, provide DNS instructions (CNAME to `realestatestar.com`). The `DomainService` validates DNS propagation.

Default: `{agent-id}.realestatestar.com`

```csharp
namespace RealEstateStar.Api.Features.Onboarding.Services;

public class DomainService(ILogger<DomainService> logger)
{
    public async Task<bool> ValidateDnsAsync(string domain, CancellationToken ct)
    {
        // Check CNAME record points to realestatestar.com
        throw new NotImplementedException();
    }
}
```

**Commit message:** `feat: add custom domain support for agent sites`

---

## Phase 13: Integration and Polish

### Task 27: End-to-end onboarding flow test

**Files:**
- Create: `apps/api/RealEstateStar.Api.Tests/Features/Onboarding/OnboardingFlowTests.cs`

Integration test that walks through the full state machine:
1. Create session with profile URL
2. Send chat messages that advance through each state
3. Verify state transitions happen in order
4. Verify session persistence across requests

```bash
cd apps/api && dotnet test --filter "OnboardingFlowTests"
```

**Commit message:** `test: add end-to-end onboarding flow integration test`

---

### Task 28: Update README and setup.sh

**Files:**
- Modify: `README.md`
- Modify: `setup.sh`

Add platform app to:
- README repo structure and tech stack
- setup.sh dependency installation
- CI pipeline documentation

**Commit message:** `docs: update README and setup.sh for platform app`

---

## Task Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1 | 1–2 | Scaffold platform app + CI |
| 2 | 3–4 | Landing page + layout |
| 3 | 5–7 | Backend domain types + state machine + session store |
| 4 | 8–10 | API endpoints as vertical slices (CreateSession, GetSession, PostChat) |
| 5 | 11–12 | Chat UI + onboard page |
| 6 | 13–18 | Rich chat card components + message renderer |
| 7 | 19 | Profile scraping |
| 8 | 20–21 | Claude API chat + tool handlers |
| 9 | 22 | Site deploy integration |
| 10 | 23 | CMA demo integration |
| 11 | 24–25 | Stripe + trial expiry |
| 12 | 26 | Custom domain support |
| 13 | 27–28 | Integration tests + docs |

**Total: 28 tasks across 13 phases**

Dependencies:
- Tasks 1–12 can be built and tested independently (stubbed backends)
- Task 19 (scraping) needs the Anthropic NuGet package
- Task 20 (Claude chat) needs an `ANTHROPIC_API_KEY` environment variable
- Task 22 (deploy) needs the agent-site template engine on main
- Task 23 (CMA demo) needs the CMA pipeline functional
- Tasks 24–25 (Stripe) need a Stripe test account and `STRIPE_SECRET_KEY`

**Key REPR conformance points:**
- All 3 endpoints are individual vertical slices under `Features/Onboarding/{Operation}/`
- Each endpoint implements `IEndpoint` — auto-discovered by `EndpointExtensions.MapEndpoints()`
- HTTP DTOs are separate from domain models (e.g. `CreateSessionRequest` ≠ `OnboardingSession`)
- All services live inside the feature: `Features/Onboarding/Services/`
- Domain types at feature root: `OnboardingSession`, `OnboardingState`, `ScrapedProfile`, `ChatMessage`
- Mappers at feature level: `OnboardingMappers.cs`
- No `Models/`, `Endpoints/`, or top-level `Services/` directories for this feature
