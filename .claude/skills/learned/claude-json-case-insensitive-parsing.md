---
name: claude-json-case-insensitive-parsing
description: "System.Text.Json is case-sensitive — Claude may return PascalCase, camelCase, or snake_case for the same field"
user-invocable: false
origin: auto-extracted
---

# Claude JSON Case-Insensitive Parsing

**Extracted:** 2026-03-29
**Context:** Parsing JSON responses from Claude API where field casing is unpredictable

## Problem
`JsonDocument.Parse` with `TryGetProperty("pricingStrategy")` is **case-sensitive**. Claude may return:
- `"pricingStrategy"` (camelCase — what we asked for)
- `"PricingStrategy"` (PascalCase)
- `"pricing_strategy"` (snake_case)

`TryGetProperty` silently returns false for case mismatches — the field appears null even though Claude returned it.

## Solution
Use a case-insensitive property lookup helper:

```csharp
private static string? GetStringPropertyCaseInsensitive(JsonElement root, string normalizedKey)
{
    foreach (var prop in root.EnumerateObject())
    {
        var key = prop.Name.Replace("_", "").ToLowerInvariant();
        if (key == normalizedKey && prop.Value.ValueKind == JsonValueKind.String)
            return prop.Value.GetString();
    }
    return null;
}

// Usage: pass the key as all-lowercase, no underscores
var pricingStrategy = GetStringPropertyCaseInsensitive(root, "pricingstrategy");
```

## When to Use
- Parsing ANY optional string field from Claude's JSON responses
- Any time `TryGetProperty` returns false but you suspect Claude returned the field
- Add `[CMA-ANALYZE-003]` style diagnostic logs showing `FieldName=True/False` to trace parsing
