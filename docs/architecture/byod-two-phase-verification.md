# BYOD custom domain two-phase verification

End-to-end Bring Your Own Domain lifecycle: TXT challenge for ownership proof, CNAME for routing activation, Cloudflare for SaaS provisioning, daily re-verification, and suspension/removal on DNS loss.

```mermaid
sequenceDiagram
    participant A as Agent
    participant P as Platform API
    participant DB as Azure Table<br/>custom-hostnames
    participant DNS as Public DNS<br/>three resolvers
    participant CF as Cloudflare<br/>for SaaS
    participant J as Background job<br/>every 5 minutes

    A->>P: POST /domains<br/>hostname jenisesellsnj.com<br/>canonical true
    P->>P: Validate hostname<br/>blocklist homoglyph Levenshtein<br/>per-account quota check
    P->>DB: Insert row<br/>status submitted<br/>VerificationCname fresh nonce
    P-->>A: CNAME challenge instructions<br/>_realstar-challenge TXT nonce
    Note over A: Agent adds TXT record<br/>at their registrar

    loop Every 5 minutes
        J->>DB: Query rows needing action
        DB-->>J: pending rows

        par Multi-resolver consensus
            J->>DNS: Resolve via 1.1.1.1
            DNS-->>J: TXT nonce
        and
            J->>DNS: Resolve via 8.8.8.8
            DNS-->>J: TXT nonce
        and
            J->>DNS: Resolve via 9.9.9.9
            DNS-->>J: TXT nonce
        end

        alt All three see the nonce
            J->>DB: Update status<br/>ownership-verified
            J->>A: Email add CNAME now<br/>point at real-estate-star.com
        else Resolver disagreement or timeout
            J->>DB: Update LastCheckedAt
            J->>J: Log DOMAIN-VERIFY-010 retry
        end
    end

    Note over A: Agent adds CNAME record
    loop Next 5 minute tick
        J->>DB: Query ownership-verified rows
        DB-->>J: row
        J->>DNS: Resolve CNAME target
        alt Points at real-estate-star.com
            J->>CF: POST custom_hostnames<br/>hostname jenisesellsnj.com
            CF-->>J: id pending ssl
            J->>DB: Update status provisioning<br/>CloudflareHostnameId
        end
    end

    loop Next 5 minute tick
        J->>CF: GET custom_hostnames id
        CF-->>J: ssl active
        J->>DB: Update status live<br/>VerifiedAt now
        J->>A: Email your site is live
    end

    Note over A: Site reachable on custom domain
    loop Daily re-verification forever
        J->>DB: Query live rows
        J->>DNS: Re-resolve CNAME
        alt Still points at us
            J->>DB: Update LastCheckedAt
        else Fails
            J->>DB: Status suspended<br/>on second consecutive failure
            J->>A: Email your custom domain stopped working
            Note over J: 7 day grace then removed plus CF delete
        end
    end
```
