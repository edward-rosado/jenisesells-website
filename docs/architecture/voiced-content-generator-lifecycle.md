# Voiced content generator lifecycle

The request/response lifecycle of a single `FieldSpec` flowing through `IVoicedContentGenerator`, including cache, schema validation, fair housing linter, retry, and fallback.

```mermaid
sequenceDiagram
    participant W as Worker or Activity
    participant G as VoicedContentGenerator
    participant C as ContentCache<br/>Azure Table
    participant A as IAnthropicClient
    participant L as IFairHousingLinter
    participant Log as Logger OTel

    W->>G: GenerateAsync VoicedRequest<br/>Facts Voice FieldSpec PipelineStep
    G->>G: Hash Facts plus FieldSpec plus Locale<br/>for cache key
    G->>C: Get cache key
    alt Cache hit within 24h
        C-->>G: Cached value
        G->>Log: VCG-010 cache hit
        G-->>W: VoicedResult value IsFallback false
    else Cache miss
        G->>G: Assemble prompt<br/>Inject voice skill system<br/>Interpolate facts user
        G->>A: SendAsync model prompt maxTokens
        alt Claude success
            A-->>G: Response tokens
            G->>G: Parse and schema validate
            alt Schema valid
                G->>L: CheckAsync output
                alt Linter passes
                    L-->>G: Clean
                    G->>C: Put cached value 24h TTL
                    G->>Log: CLAUDE-020 with PipelineStep<br/>VCG-020 schema pass
                    G-->>W: VoicedResult value
                else Linter flags
                    L-->>G: Flagged phrase category
                    G->>A: SendAsync regenerate with<br/>anti-steering instruction
                    A-->>G: Retry response
                    G->>L: CheckAsync again
                    alt Retry clean
                        L-->>G: Clean
                        G-->>W: VoicedResult value
                    else Retry flagged
                        L-->>G: Still flagged
                        G->>Log: FHA-001 VCG-040 fallback
                        G-->>W: VoicedResult FallbackValue<br/>IsFallback true
                    end
                end
            else Schema invalid
                G->>Log: VCG-030 schema fail
                G-->>W: VoicedResult FallbackValue
            end
        else Claude error after retries
            A-->>G: 429 or 5xx exhausted
            G->>Log: CLAUDE-FAIL VCG-030
            G-->>W: VoicedResult FallbackValue<br/>IsFallback true
        end
    end
```
