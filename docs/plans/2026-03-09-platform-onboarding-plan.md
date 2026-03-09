# Platform & Onboarding Chat UI — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the Real Estate Star marketing website with an AI-powered onboarding chat that scrapes agent profiles, deploys their white-label site, demos the CMA pipeline, and collects payment — all in one conversation.

**Architecture:** Next.js 16 frontend (`apps/platform/`) renders a landing page and full-screen chat UI. .NET Minimal API (`apps/api/`) owns the onboarding state machine, Claude API integration, profile scraping, and session persistence. Chat streams via SSE. Stripe handles deferred payment.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, .NET 8 Minimal API, Claude API, Stripe, SSE

**Design Doc:** `docs/plans/2026-03-09-platform-onboarding-design.md`

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

**Step 2: Update branch protection to require platform check**

```bash
gh api repos/{owner}/{repo}/branches/main/protection \
  --method PUT \
  --input - <<'EOF'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "Agent Site — lint, test, coverage",
      "API — build and test",
      "Platform — lint, test, coverage"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0
  },
  "restrictions": null
}
EOF
```

**Step 3: Commit**

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

  it("renders the Log In link", () => {
    render(<LandingPage />);
    expect(screen.getByRole("link", { name: /Log In/i })).toBeInTheDocument();
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

Expected: PASS (adjust test for Log In link placement if needed).

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

// Mock next/link
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

**Step 2: Run test to verify it fails**

```bash
cd apps/platform && npx vitest run __tests__/layout.test.tsx
```

**Step 3: Implement the layout**

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

**Step 4: Run test to verify it passes**

```bash
cd apps/platform && npx vitest run __tests__/layout.test.tsx
```

**Step 5: Remove Log In test from landing test if it moved to layout**

Update `__tests__/landing.test.tsx` — remove the "renders the Log In link" test since it's now in the layout.

**Step 6: Commit**

```bash
git add apps/platform/app/layout.tsx apps/platform/__tests__/
git commit -m "feat: add layout with brand header and Log In link"
```

---

## Phase 3: Backend Onboarding State Machine

### Task 5: Onboarding state enum and session model

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Models/OnboardingState.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Models/OnboardingSession.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Models/ScrapedProfile.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/OnboardingSessionTests.cs`

**Step 1: Write the failing test**

Create `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/OnboardingSessionTests.cs`:

```csharp
using RealEstateStar.Api.Onboarding.Models;
using Xunit;

namespace RealEstateStar.Api.Tests.Onboarding;

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

**Step 3: Create the models**

Create `apps/api/src/RealEstateStar.Api/Onboarding/Models/OnboardingState.cs`:

```csharp
namespace RealEstateStar.Api.Onboarding.Models;

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

Create `apps/api/src/RealEstateStar.Api/Onboarding/Models/ScrapedProfile.cs`:

```csharp
namespace RealEstateStar.Api.Onboarding.Models;

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

Create `apps/api/src/RealEstateStar.Api/Onboarding/Models/OnboardingSession.cs`:

```csharp
using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Onboarding.Models;

public sealed record ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

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

Expected: PASS

**Step 5: Commit**

```bash
git add apps/api/src/RealEstateStar.Api/Onboarding/ apps/api/tests/
git commit -m "feat: add onboarding state enum, session, and profile models"
```

---

### Task 6: State machine with transitions and tool access

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/OnboardingStateMachine.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/StateMachineTests.cs`

**Step 1: Write the failing test**

Create `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/StateMachineTests.cs`:

```csharp
using RealEstateStar.Api.Onboarding.Models;
using RealEstateStar.Api.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Onboarding;

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
        Assert.Contains("parse_profile", tools);
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

Create `apps/api/src/RealEstateStar.Api/Onboarding/Services/OnboardingStateMachine.cs`:

```csharp
using RealEstateStar.Api.Onboarding.Models;

namespace RealEstateStar.Api.Onboarding.Services;

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
        [OnboardingState.ScrapeProfile] = ["scrape_url", "parse_profile"],
        [OnboardingState.ConfirmIdentity] = ["update_profile"],
        [OnboardingState.CollectBranding] = ["extract_colors", "set_branding"],
        [OnboardingState.GenerateSite] = ["create_config", "deploy_site"],
        [OnboardingState.PreviewSite] = ["get_preview_url"],
        [OnboardingState.DemoCma] = ["submit_cma_form"],
        [OnboardingState.ShowResults] = ["check_inbox", "check_drive"],
        [OnboardingState.CollectPayment] = ["create_stripe_session"],
        [OnboardingState.TrialActivated] = ["get_site_url"],
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

Expected: PASS

**Step 5: Commit**

```bash
git add apps/api/src/RealEstateStar.Api/Onboarding/Services/ apps/api/tests/
git commit -m "feat: add onboarding state machine with transitions and tool access"
```

---

### Task 7: Session persistence service (JSON files)

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/SessionStore.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/SessionStoreTests.cs`

**Step 1: Write the failing test**

Create `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/SessionStoreTests.cs`:

```csharp
using RealEstateStar.Api.Onboarding.Models;
using RealEstateStar.Api.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Onboarding;

public class SessionStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly SessionStore _store;

    public SessionStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-sessions-{Guid.NewGuid():N}");
        _store = new SessionStore(_testDir);
    }

    public void Dispose() => Directory.Delete(_testDir, true);

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync(session.Id);
        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded!.Id);
        Assert.Equal(session.ProfileUrl, loaded.ProfileUrl);
    }

    [Fact]
    public async Task Load_NonExistentId_ReturnsNull()
    {
        var result = await _store.LoadAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_PreservesMessages()
    {
        var session = OnboardingSession.Create(null);
        session.Messages.Add(new ChatMessage { Role = "user", Content = "hello" });
        session.Messages.Add(new ChatMessage { Role = "assistant", Content = "hi" });
        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync(session.Id);
        Assert.Equal(2, loaded!.Messages.Count);
        Assert.Equal("hello", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task Save_PreservesStateChanges()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.CollectBranding;
        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync(session.Id);
        Assert.Equal(OnboardingState.CollectBranding, loaded!.CurrentState);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "SessionStoreTests"
```

**Step 3: Implement the session store**

Create `apps/api/src/RealEstateStar.Api/Onboarding/Services/SessionStore.cs`:

```csharp
using System.Text.Json;
using RealEstateStar.Api.Onboarding.Models;

namespace RealEstateStar.Api.Onboarding.Services;

public class SessionStore(string basePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SessionStore() : this(Path.Combine(
        AppContext.BaseDirectory, "data", "sessions")) { }

    public async Task SaveAsync(OnboardingSession session, CancellationToken ct = default)
    {
        Directory.CreateDirectory(basePath);
        var path = GetPath(session.Id);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public async Task<OnboardingSession?> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<OnboardingSession>(json, JsonOptions);
    }

    private string GetPath(string sessionId) => Path.Combine(basePath, $"{sessionId}.json");
}
```

**Step 4: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "SessionStoreTests"
```

**Step 5: Commit**

```bash
git add apps/api/src/RealEstateStar.Api/Onboarding/Services/SessionStore.cs \
  apps/api/tests/RealEstateStar.Api.Tests/Onboarding/SessionStoreTests.cs
git commit -m "feat: add JSON file-based session persistence"
```

---

## Phase 4: Onboarding API Endpoints

### Task 8: Create session and chat endpoints with SSE

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Endpoints/OnboardingEndpoints.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/OnboardingEndpointsTests.cs`
- Modify: `apps/api/src/RealEstateStar.Api/Program.cs`

**Step 1: Write the failing test**

Create `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/OnboardingEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RealEstateStar.Api.Tests.Onboarding;

public class OnboardingEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OnboardingEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateSession_ReturnsSessionId()
    {
        var response = await _client.PostAsJsonAsync("/onboard", new { profileUrl = "https://zillow.com/profile/test" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.SessionId));
    }

    [Fact]
    public async Task CreateSession_WithoutUrl_ReturnsSessionId()
    {
        var response = await _client.PostAsJsonAsync("/onboard", new { profileUrl = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_ValidId_ReturnsSession()
    {
        var createResponse = await _client.PostAsJsonAsync("/onboard", new { profileUrl = (string?)null });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateSessionResponse>();

        var response = await _client.GetAsync($"/onboard/{created!.SessionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_InvalidId_Returns404()
    {
        var response = await _client.GetAsync("/onboard/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public record CreateSessionResponse(string SessionId);
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api && dotnet test --filter "OnboardingEndpointsTests"
```

**Step 3: Implement the endpoints**

Create `apps/api/src/RealEstateStar.Api/Onboarding/Endpoints/OnboardingEndpoints.cs`:

```csharp
using RealEstateStar.Api.Onboarding.Models;
using RealEstateStar.Api.Onboarding.Services;

namespace RealEstateStar.Api.Onboarding.Endpoints;

public static class OnboardingEndpoints
{
    public static void MapOnboardingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/onboard");

        group.MapPost("/", CreateSession);
        group.MapGet("/{sessionId}", GetSession);
        group.MapPost("/{sessionId}/chat", PostChat);
    }

    private static async Task<IResult> CreateSession(
        CreateSessionRequest request,
        SessionStore store,
        CancellationToken ct)
    {
        var session = OnboardingSession.Create(request.ProfileUrl);
        await store.SaveAsync(session, ct);
        return Results.Ok(new { sessionId = session.Id });
    }

    private static async Task<IResult> GetSession(
        string sessionId,
        SessionStore store,
        CancellationToken ct)
    {
        var session = await store.LoadAsync(sessionId, ct);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }

    private static async Task PostChat(
        string sessionId,
        ChatRequest request,
        SessionStore store,
        OnboardingStateMachine stateMachine,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var session = await store.LoadAsync(sessionId, ct);
        if (session is null)
        {
            httpContext.Response.StatusCode = 404;
            return;
        }

        session.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = request.Message
        });

        // SSE response
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // TODO: Wire Claude API in Task 10
        // For now, echo the allowed tools for the current state
        var tools = stateMachine.GetAllowedTools(session.CurrentState);
        var reply = $"[State: {session.CurrentState}] Tools available: {string.Join(", ", tools)}";

        session.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = reply
        });

        await store.SaveAsync(session, ct);

        await httpContext.Response.WriteAsync($"data: {reply}\n\n", ct);
        await httpContext.Response.WriteAsync("data: [DONE]\n\n", ct);
    }
}

public sealed record CreateSessionRequest(string? ProfileUrl);
public sealed record ChatRequest(string Message);
```

**Step 4: Register services and endpoints in Program.cs**

Add to `apps/api/src/RealEstateStar.Api/Program.cs` before `var app = builder.Build();`:

```csharp
// Onboarding services
builder.Services.AddSingleton<OnboardingStateMachine>();
builder.Services.AddSingleton<SessionStore>();
```

Add after `app.MapGet("/", ...)`:

```csharp
app.MapOnboardingEndpoints();
```

Add using directives:

```csharp
using RealEstateStar.Api.Onboarding.Endpoints;
using RealEstateStar.Api.Onboarding.Services;
```

**Step 5: Run test to verify it passes**

```bash
cd apps/api && dotnet test --filter "OnboardingEndpointsTests"
```

**Step 6: Commit**

```bash
git add apps/api/
git commit -m "feat: add onboarding API endpoints with session CRUD and SSE chat stub"
```

---

## Phase 5: Chat UI (Frontend)

### Task 9: Chat window and message components

**Files:**
- Create: `apps/platform/components/chat/ChatWindow.tsx`
- Create: `apps/platform/components/chat/MessageBubble.tsx`
- Create: `apps/platform/components/chat/ActionButton.tsx`
- Create: `apps/platform/components/chat/ProgressIndicator.tsx`
- Create: `apps/platform/lib/types.ts`
- Create: `apps/platform/__tests__/components/ChatWindow.test.tsx`
- Create: `apps/platform/__tests__/components/MessageBubble.test.tsx`

**Step 1: Create shared types**

Create `apps/platform/lib/types.ts`:

```typescript
export type MessageType =
  | "text"
  | "profile_card"
  | "color_palette"
  | "site_preview"
  | "email_preview"
  | "drive_preview"
  | "feature_checklist"
  | "payment"
  | "progress";

export interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  type: MessageType;
  content: string;
  data?: Record<string, unknown>;
  timestamp: string;
}

export interface OnboardingSession {
  sessionId: string;
  currentState: string;
  messages: ChatMessage[];
}
```

**Step 2: Write failing tests**

Create `apps/platform/__tests__/components/MessageBubble.test.tsx`:

```typescript
import { render, screen } from "@testing-library/react";
import { MessageBubble } from "../../components/chat/MessageBubble";

describe("MessageBubble", () => {
  it("renders user message aligned right", () => {
    render(<MessageBubble role="user" content="hello" />);
    const bubble = screen.getByText("hello");
    expect(bubble.closest("div")).toHaveClass("ml-auto");
  });

  it("renders assistant message aligned left", () => {
    render(<MessageBubble role="assistant" content="hi there" />);
    const bubble = screen.getByText("hi there");
    expect(bubble.closest("div")).not.toHaveClass("ml-auto");
  });
});
```

Create `apps/platform/__tests__/components/ChatWindow.test.tsx`:

```typescript
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatWindow } from "../../components/chat/ChatWindow";

describe("ChatWindow", () => {
  it("renders the message input", () => {
    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);
    expect(screen.getByPlaceholderText(/Type a message/i)).toBeInTheDocument();
  });

  it("renders the send button", () => {
    render(<ChatWindow sessionId="test-123" initialMessages={[]} />);
    expect(screen.getByRole("button", { name: /Send/i })).toBeInTheDocument();
  });

  it("renders initial messages", () => {
    const messages = [
      { id: "1", role: "assistant" as const, type: "text" as const, content: "Welcome!", timestamp: new Date().toISOString() },
    ];
    render(<ChatWindow sessionId="test-123" initialMessages={messages} />);
    expect(screen.getByText("Welcome!")).toBeInTheDocument();
  });
});
```

**Step 3: Run tests to verify they fail**

```bash
cd apps/platform && npx vitest run __tests__/components/
```

**Step 4: Implement the components**

Create `apps/platform/components/chat/MessageBubble.tsx`:

```typescript
interface MessageBubbleProps {
  role: "user" | "assistant";
  content: string;
}

export function MessageBubble({ role, content }: MessageBubbleProps) {
  const isUser = role === "user";
  return (
    <div className={`max-w-[80%] ${isUser ? "ml-auto" : ""}`}>
      <div
        className={`rounded-2xl px-4 py-3 ${
          isUser
            ? "bg-emerald-600 text-white"
            : "bg-gray-800 text-gray-100"
        }`}
      >
        {content}
      </div>
    </div>
  );
}
```

Create `apps/platform/components/chat/ActionButton.tsx`:

```typescript
interface ActionButtonProps {
  label: string;
  onClick: () => void;
  variant?: "primary" | "secondary";
}

export function ActionButton({ label, onClick, variant = "primary" }: ActionButtonProps) {
  return (
    <button
      onClick={onClick}
      className={`px-4 py-2 rounded-lg font-medium transition-colors ${
        variant === "primary"
          ? "bg-emerald-600 hover:bg-emerald-500 text-white"
          : "bg-gray-700 hover:bg-gray-600 text-gray-200"
      }`}
    >
      {label}
    </button>
  );
}
```

Create `apps/platform/components/chat/ProgressIndicator.tsx`:

```typescript
interface ProgressIndicatorProps {
  message: string;
}

export function ProgressIndicator({ message }: ProgressIndicatorProps) {
  return (
    <div className="flex items-center gap-3 px-4 py-3 bg-gray-800 rounded-2xl max-w-[80%]">
      <div className="h-4 w-4 rounded-full border-2 border-emerald-500 border-t-transparent animate-spin" />
      <span className="text-gray-300">{message}</span>
    </div>
  );
}
```

Create `apps/platform/components/chat/ChatWindow.tsx`:

```typescript
"use client";

import { useState, useRef, useEffect } from "react";
import { MessageBubble } from "./MessageBubble";
import type { ChatMessage } from "@/lib/types";

interface ChatWindowProps {
  sessionId: string;
  initialMessages: ChatMessage[];
}

export function ChatWindow({ sessionId, initialMessages }: ChatWindowProps) {
  const [messages, setMessages] = useState<ChatMessage[]>(initialMessages);
  const [input, setInput] = useState("");
  const [isStreaming, setIsStreaming] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  async function handleSend() {
    const text = input.trim();
    if (!text || isStreaming) return;

    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: "user",
      type: "text",
      content: text,
      timestamp: new Date().toISOString(),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setIsStreaming(true);

    try {
      const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
      const response = await fetch(`${apiUrl}/onboard/${sessionId}/chat`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message: text }),
      });

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();
      let accumulated = "";

      if (reader) {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          const chunk = decoder.decode(value);
          const lines = chunk.split("\n");
          for (const line of lines) {
            if (line.startsWith("data: ") && !line.includes("[DONE]")) {
              accumulated += line.slice(6);
            }
          }
        }
      }

      if (accumulated) {
        setMessages((prev) => [
          ...prev,
          {
            id: crypto.randomUUID(),
            role: "assistant",
            type: "text",
            content: accumulated,
            timestamp: new Date().toISOString(),
          },
        ]);
      }
    } finally {
      setIsStreaming(false);
    }
  }

  return (
    <div className="flex flex-col h-screen bg-gray-950">
      <div className="flex-1 overflow-y-auto px-4 py-6 space-y-4">
        {messages.map((msg) => (
          <MessageBubble key={msg.id} role={msg.role} content={msg.content} />
        ))}
        <div ref={bottomRef} />
      </div>
      <div className="border-t border-gray-800 px-4 py-3">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleSend();
          }}
          className="flex gap-2"
        >
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            placeholder="Type a message..."
            disabled={isStreaming}
            className="flex-1 px-4 py-3 rounded-lg bg-gray-800 border border-gray-700 text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-50"
          />
          <button
            type="submit"
            disabled={isStreaming || !input.trim()}
            className="px-6 py-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition-colors disabled:opacity-50"
          >
            Send
          </button>
        </form>
      </div>
    </div>
  );
}
```

**Step 5: Run tests to verify they pass**

```bash
cd apps/platform && npx vitest run __tests__/components/
```

**Step 6: Commit**

```bash
git add apps/platform/components/ apps/platform/lib/ apps/platform/__tests__/
git commit -m "feat: add chat window, message bubble, action button, and progress components"
```

---

### Task 10: Onboard page that creates session and renders chat

**Files:**
- Create: `apps/platform/app/onboard/page.tsx`
- Create: `apps/platform/__tests__/onboard.test.tsx`

**Step 1: Write the failing test**

Create `apps/platform/__tests__/onboard.test.tsx`:

```typescript
import { render, screen } from "@testing-library/react";
import OnboardPage from "../app/onboard/page";

// Mock ChatWindow since it needs a real API
vi.mock("../components/chat/ChatWindow", () => ({
  ChatWindow: ({ sessionId }: { sessionId: string }) => (
    <div data-testid="chat-window" data-session={sessionId}>
      mock chat
    </div>
  ),
}));

// Mock useSearchParams and useRouter
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams("profileUrl=https://zillow.com/profile/test"),
  useRouter: () => ({ push: vi.fn() }),
}));

describe("OnboardPage", () => {
  it("renders the chat window or loading state", () => {
    render(<OnboardPage />);
    // Initially shows loading while session is being created
    expect(
      screen.getByText(/mock chat/i) || screen.getByText(/loading/i)
    ).toBeTruthy();
  });
});
```

**Step 2: Implement the onboard page**

Create `apps/platform/app/onboard/page.tsx`:

```typescript
"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { ChatWindow } from "@/components/chat/ChatWindow";

export default function OnboardPage() {
  const searchParams = useSearchParams();
  const profileUrl = searchParams.get("profileUrl");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function createSession() {
      try {
        const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
        const response = await fetch(`${apiUrl}/onboard`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ profileUrl }),
        });
        const data = await response.json();
        setSessionId(data.sessionId);
      } catch {
        setError("Failed to start onboarding. Please try again.");
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

**Step 3: Run tests**

```bash
cd apps/platform && npx vitest run __tests__/onboard.test.tsx
```

**Step 4: Commit**

```bash
git add apps/platform/app/onboard/ apps/platform/__tests__/onboard.test.tsx
git commit -m "feat: add onboard page with session creation and chat rendering"
```

---

## Phase 6: Rich Chat Cards

### Task 11: Profile card component

**Files:**
- Create: `apps/platform/components/chat/ProfileCard.tsx`
- Create: `apps/platform/__tests__/components/ProfileCard.test.tsx`

**Step 1: Write the failing test**

```typescript
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ProfileCard } from "../../components/chat/ProfileCard";

const profile = {
  name: "Jenise Buckalew",
  brokerage: "RE/MAX",
  state: "NJ",
  serviceAreas: ["Middlesex County"],
  photoUrl: "https://example.com/photo.jpg",
  reviewCount: 47,
  homesSold: 120,
};

describe("ProfileCard", () => {
  it("renders agent name", () => {
    render(<ProfileCard profile={profile} onConfirm={vi.fn()} />);
    expect(screen.getByText("Jenise Buckalew")).toBeInTheDocument();
  });

  it("renders brokerage", () => {
    render(<ProfileCard profile={profile} onConfirm={vi.fn()} />);
    expect(screen.getByText(/RE\/MAX/)).toBeInTheDocument();
  });

  it("renders confirm button", () => {
    render(<ProfileCard profile={profile} onConfirm={vi.fn()} />);
    expect(screen.getByRole("button", { name: /Looks right/i })).toBeInTheDocument();
  });

  it("calls onConfirm when button clicked", async () => {
    const onConfirm = vi.fn();
    render(<ProfileCard profile={profile} onConfirm={onConfirm} />);
    await userEvent.click(screen.getByRole("button", { name: /Looks right/i }));
    expect(onConfirm).toHaveBeenCalled();
  });
});
```

**Step 2–5: Implement, test, commit** — follow same TDD pattern.

Component renders a card with photo, name, brokerage, state, stats, and a "Looks right" action button.

**Commit message:** `feat: add profile card chat component`

---

### Task 12: Color palette component

**Files:**
- Create: `apps/platform/components/chat/ColorPalette.tsx`
- Create: `apps/platform/__tests__/components/ColorPalette.test.tsx`

Renders extracted branding colors as swatches with labels. Includes "Customize" button that allows editing. Calls `onConfirm` with final color selections.

**Commit message:** `feat: add color palette chat component`

---

### Task 13: Site preview component

**Files:**
- Create: `apps/platform/components/chat/SitePreview.tsx`
- Create: `apps/platform/__tests__/components/SitePreview.test.tsx`

Renders a responsive iframe showing the deployed agent site. Includes "Approve" button.

**Commit message:** `feat: add site preview iframe chat component`

---

### Task 14: Feature checklist component

**Files:**
- Create: `apps/platform/components/chat/FeatureChecklist.tsx`
- Create: `apps/platform/__tests__/components/FeatureChecklist.test.tsx`

Renders the platform capabilities list with checkmarks. This is the "wow expansion" moment — shows what else Real Estate Star automates beyond the CMA they just experienced.

Features to display:
- Instant CMA for every lead
- Contract drafting and DocuSign
- MLS listing creation
- Lead tracking and follow-up
- Everything organized in your Google Drive

**Commit message:** `feat: add feature checklist chat component`

---

### Task 15: Payment card component (Stripe stub)

**Files:**
- Create: `apps/platform/components/chat/PaymentCard.tsx`
- Create: `apps/platform/__tests__/components/PaymentCard.test.tsx`

For now, renders a styled card with "$900 — One Time" and a "Start Free Trial" button. Stripe Elements integration will be wired in Phase 8. The button calls `onPaymentComplete` prop.

**Commit message:** `feat: add payment card chat component (Stripe stub)`

---

### Task 16: Message renderer that dispatches to card components

**Files:**
- Create: `apps/platform/components/chat/MessageRenderer.tsx`
- Create: `apps/platform/__tests__/components/MessageRenderer.test.tsx`
- Modify: `apps/platform/components/chat/ChatWindow.tsx`

The `MessageRenderer` takes a `ChatMessage` and renders the appropriate component based on `type`: text → `MessageBubble`, profile_card → `ProfileCard`, color_palette → `ColorPalette`, etc.

Update `ChatWindow` to use `MessageRenderer` instead of `MessageBubble` directly.

**Commit message:** `feat: add message renderer that dispatches to chat card components`

---

## Phase 7: Profile Scraping

### Task 17: Profile scraper service (AI-based)

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/ProfileScraperService.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/ProfileScraperTests.cs`

Start with the AI scraper (handles any URL). Fetch the page HTML, send to Claude API with a structured prompt, parse the response into `ScrapedProfile`.

**Step 1: Write the failing test**

Test with a mock HTTP client and mock Claude response. Verify:
- Returns `ScrapedProfile` with extracted fields
- Handles fetch failures gracefully (returns partial profile)
- Handles non-real-estate pages (returns null)

**Step 2: Implement**

```csharp
public class ProfileScraperService(HttpClient httpClient, ILogger<ProfileScraperService> logger)
{
    public async Task<ScrapedProfile?> ScrapeAsync(string url, CancellationToken ct)
    {
        // 1. Fetch page HTML
        // 2. Strip scripts/styles, extract text content
        // 3. Send to Claude API with extraction prompt
        // 4. Parse structured response into ScrapedProfile
    }
}
```

The Claude prompt should request JSON output matching `ScrapedProfile` fields.

**NuGet dependency:** Add `Anthropic` SDK package to the csproj.

```bash
cd apps/api && dotnet add src/RealEstateStar.Api/RealEstateStar.Api.csproj package Anthropic
```

**Commit message:** `feat: add AI-based profile scraper service`

---

## Phase 8: Claude Chat Service

### Task 18: Claude API chat service with state-aware tools

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/OnboardingChatService.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/ChatServiceTests.cs`
- Modify: `apps/api/src/RealEstateStar.Api/Onboarding/Endpoints/OnboardingEndpoints.cs`

**Step 1: Write the failing test**

Test that:
- Service builds Claude messages from session history
- Only includes tools allowed by current state
- System prompt includes onboarding context and agent profile
- Streams response chunks

**Step 2: Implement**

```csharp
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
    }

    private static string BuildSystemPrompt(OnboardingSession session)
    {
        // Include current state, profile data, instructions per state
    }
}
```

**Step 3: Wire into endpoints**

Replace the TODO stub in `OnboardingEndpoints.PostChat` with calls to `OnboardingChatService.StreamResponseAsync`.

**Commit message:** `feat: add Claude API chat service with state-aware tool access`

---

### Task 19: Tool execution handlers

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/ToolHandler.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/ScrapeUrlTool.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/UpdateProfileTool.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/SetBrandingTool.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/DeploySiteTool.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/ToolHandlerTests.cs`

Each tool is a handler that receives parameters from Claude's tool_use response, executes the action, and returns a result. The `ToolHandler` dispatches to the correct handler by tool name.

```csharp
public interface IOnboardingTool
{
    string Name { get; }
    Task<string> ExecuteAsync(JsonElement parameters, OnboardingSession session, CancellationToken ct);
}
```

**Commit message:** `feat: add onboarding tool execution handlers`

---

## Phase 9: Site Deploy Integration

### Task 20: Site deploy service — generate config and deploy

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/SiteDeployService.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/SiteDeployTests.cs`

This service:
1. Generates `{agent-id}.json` config from `ScrapedProfile` + branding choices
2. Generates `{agent-id}.content.json` with default content sections
3. Creates a git branch `onboard/{agent-id}`
4. Commits the config files
5. Opens a PR via GitHub API
6. Returns the preview URL

For v1, this can write config files locally and trigger a build. The full PR pipeline can be wired later.

**Commit message:** `feat: add site deploy service for onboarding`

---

## Phase 10: CMA Demo Integration

### Task 21: CMA demo trigger in onboarding

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/SubmitCmaFormTool.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/CmaToolTests.cs`

This tool:
1. Pre-fills a CMA form submission with a sample property address
2. Submits it to the existing CMA pipeline endpoint
3. Returns status updates as the pipeline runs
4. Tells the agent to check their inbox and Drive

Depends on the CMA pipeline being functional (built in the other session).

**Commit message:** `feat: add CMA demo trigger tool for onboarding`

---

## Phase 11: Stripe Integration

### Task 22: Stripe SetupIntent service

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/StripeService.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Tools/CreateStripeSessionTool.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/StripeServiceTests.cs`
- Modify: `apps/platform/components/chat/PaymentCard.tsx`

**Step 1: Add Stripe NuGet package**

```bash
cd apps/api && dotnet add src/RealEstateStar.Api/RealEstateStar.Api.csproj package Stripe.net
```

**Step 2: Add Stripe.js to frontend**

```bash
cd apps/platform && npm install @stripe/stripe-js @stripe/react-stripe-js
```

**Step 3: Implement StripeService**

```csharp
public class StripeService(IConfiguration config, ILogger<StripeService> logger)
{
    public async Task<string> CreateSetupIntentAsync(string sessionId, CancellationToken ct)
    {
        // Create Stripe SetupIntent
        // Return client secret for frontend
    }

    public async Task ChargeAsync(string setupIntentId, CancellationToken ct)
    {
        // Called by scheduled job on day 7
        // Creates PaymentIntent for $900 using saved payment method
    }
}
```

**Step 4: Update PaymentCard to use Stripe Elements**

Replace the stub button with `@stripe/react-stripe-js` `Elements` + `PaymentElement`.

**Commit message:** `feat: add Stripe SetupIntent integration for deferred payment`

---

### Task 23: Trial expiry scheduled job

**Files:**
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/TrialExpiryService.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/TrialExpiryTests.cs`

A background service that runs daily:
1. Scans sessions in `TrialActivated` state
2. If trial started >= 7 days ago and not cancelled → charge via StripeService
3. If cancelled → deactivate site, clean up

Use `IHostedService` / `BackgroundService` for the scheduled job.

**Commit message:** `feat: add trial expiry background service`

---

## Phase 12: Custom Domain Support

### Task 24: Custom domain configuration

**Files:**
- Modify: `apps/api/src/RealEstateStar.Api/Onboarding/Models/OnboardingSession.cs`
- Create: `apps/api/src/RealEstateStar.Api/Onboarding/Services/DomainService.cs`
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/DomainServiceTests.cs`

Add `CustomDomain` field to session. During the `TrialActivated` state, the AI asks if the agent has a custom domain. If yes, provide DNS instructions (CNAME to `realestatestar.com`). The `DomainService` validates DNS propagation.

Default: `{agent-id}.realestatestar.com`

**Commit message:** `feat: add custom domain support for agent sites`

---

## Phase 13: Integration and Polish

### Task 25: End-to-end onboarding flow test

**Files:**
- Create: `apps/api/tests/RealEstateStar.Api.Tests/Onboarding/OnboardingFlowTests.cs`

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

### Task 26: Update README and setup.sh

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
| 3 | 5–7 | Backend state machine + session persistence |
| 4 | 8 | API endpoints with SSE |
| 5 | 9–10 | Chat UI + onboard page |
| 6 | 11–16 | Rich chat card components |
| 7 | 17 | Profile scraping |
| 8 | 18–19 | Claude API chat + tool handlers |
| 9 | 20 | Site deploy integration |
| 10 | 21 | CMA demo integration |
| 11 | 22–23 | Stripe + trial expiry |
| 12 | 24 | Custom domain support |
| 13 | 25–26 | Integration tests + docs |

**Total: 26 tasks across 13 phases**

Dependencies:
- Tasks 1–10 can be built and tested independently (stubbed backends)
- Task 17 (scraping) needs the Anthropic NuGet package
- Task 18 (Claude chat) needs an `ANTHROPIC_API_KEY` environment variable
- Task 20 (deploy) needs the agent-site template engine on main
- Task 21 (CMA demo) needs the CMA pipeline functional
- Tasks 22–23 (Stripe) need a Stripe test account and `STRIPE_SECRET_KEY`
