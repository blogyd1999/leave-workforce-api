# Review: version and reality verification (as of 2026-10-12)

Lens: was each committed technology/claim web-researched, not asserted? Spine and memlog read; independent web checks done. Status per item: confirmed / unconfirmed / wrong.

## Verdict
Mostly sound. The core stack is real, current and fits. No wrong claims, no critical issues. Gaps: stale or loose pins (xunit.v3, SDK, Testcontainers), the SELECT ... FOR UPDATE mechanism is not named, HS256 runs against Microsoft's current guidance and is not justified, the token-minting library is unnamed, and Konscious is unreleased for 28 months. Counts: critical 0, high 2, medium 5, low 5.

## Verification table

| Item | Status | Evidence |
| --- | --- | --- |
| .NET 10 LTS | confirmed | LTS to Nov 2028. Day differs by source (10 Nov in announcement vs 14 Nov in dotnet/core README); memlog's 2028-11-14 matches README. https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md |
| EF Core 10.0.x | confirmed | Latest patch appears to be 10.0.12 (GitHub releases); spine's "at least 10.0.4" is correct as a floor. https://github.com/dotnet/efcore/releases |
| Npgsql.EFCore.PostgreSQL 10.0.3, needs EF >= 10.0.4 | confirmed | NuGet: 10.0.3 listed 7/10/2026 is latest stable; deps Microsoft.EntityFrameworkCore(.Relational) >= 10.0.4 < 11, Npgsql >= 10.0.3. https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL (11.0.0-rc.1 exists, prerelease only) |
| PostgreSQL 18 (18.6, support to 2030-11-14) | confirmed | 18.6 released 2026-08-13 (18.5 skipped); policy table gives 14 Nov 2030. https://www.postgresql.org/support/versioning/ |
| Konscious.Argon2 1.3.1 | confirmed current, but stale | Latest is 1.3.1, published 2024-06-19, no newer release in about 28 months. https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2 |
| Better Argon2id option | unconfirmed | Argon2Sharp (Rust FFI, one contributor, Jan 2026) is experimental; Isopoh maintenance not established; Argon2id.PasswordHasher just wraps Konscious. Nothing clearly better found. https://github.com/Paol0B/Argon2Sharp |
| xunit.v3 4.0.x | confirmed, loose | Latest 4.0.2 (2026-10-09); 4.0.0 only 2026-08-15. https://www.nuget.org/packages/xunit.v3 |
| Testcontainers.PostgreSql 4.15.0 | confirmed, possibly one behind | 4.15.0 (2026-09-06) verified; a 2026-10-08 Renovate PR references 4.16.0 but not seen on NuGet. https://www.nuget.org/packages/Testcontainers.PostgreSql |
| JwtBearer 10.0.x | confirmed | 10.0.12 on NuGet (2026-09-08). https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer |
| Microsoft.AspNetCore.OpenApi | confirmed | Built in, docs updated 2026-10. Build-time emission needs the separate Microsoft.Extensions.ApiDescription.Server package (not named in spine). https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0 |
| Built-in rate limiter, partition by connection IP | confirmed | Microsoft.AspNetCore.RateLimiting; docs show `Connection.RemoteIpAddress` partitioning and warn IP/user-input partitions are a DoS/memory risk (spoofing). In-memory per instance. https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0 |
| Minimal APIs, FallbackPolicy | confirmed | `SetFallbackPolicy` documented on the JWT page. https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0 |
| SQLSTATE 23505 / 40001 / 40P01 | confirmed | https://www.postgresql.org/docs/18/errcodes-appendix.html |
| pg_advisory_xact_lock | confirmed | bigint and (int,int) overloads, released at transaction end, two key spaces do not overlap. https://www.postgresql.org/docs/18/functions-admin.html |
| SELECT ... FOR UPDATE in EF Core 10 | confirmed possible, mechanism unstated | No LINQ support (dotnet/efcore#26042 open); only raw SQL (FromSql/ExecuteSql) inside an explicit transaction. https://github.com/dotnet/efcore/issues/26042 |
| One DbTransaction shared by several DbContexts | confirmed | Contexts share one DbConnection, then `Database.UseTransactionAsync(dbTransaction)`; relational only (docs sample is SQL Server; same API applies to Npgsql). https://learn.microsoft.com/ef/core/saving/transactions |
| HS256 JwtBearer config "current practice" | unconfirmed / contradicted | Works, but Microsoft's JWT page says asymmetric keys should "always" be used when creating tokens and not to hand-roll tokens from username/password. See finding H2. |
| TimeZoneInfo IANA ids on macOS/Linux, .NET 10 | confirmed with caveat | IANA ids work on Linux/macOS; need tzdata in the image (not in some minimal/Alpine images), and invariant-globalization mode affects ID conversion. https://learn.microsoft.com/dotnet/api/system.timezoneinfo.findsystemtimezonebyid?view=net-10.0 |

## Findings

### High
- **H1 - FOR UPDATE mechanism not committed (AD-4).** The spine mandates `SELECT ... FOR UPDATE` but never states how; EF Core 10 has no built-in support, so every handler needs raw `FromSql`/`ExecuteSql` inside the shared transaction. Gotchas: tracked-entity staleness (an already-tracked row is not refreshed by the locking query), unbounded lock waits, EF execution strategy (`EnableRetryOnFailure`) rejects manual transactions. Fix: add to AD-3/AD-4 one sentence: locks are taken via parameterised `FromSqlInterpolated(... FOR UPDATE)` in a `ILockingReader` in BuildingBlocks; `SET LOCAL lock_timeout` per transaction; no `EnableRetryOnFailure` (retry is the handler-level retry of AD-4); lock queries use `AsNoTracking` or load before any other read of the row.
- **H2 - HS256 asserted without reality check (AD-6).** Microsoft's current JWT guidance says asymmetric keys should always be used for creating access tokens. HS256 with a rotated key list is defensible for a single-service API that both mints and validates, but the spine states it as a given. Fix: add a one-line rationale in AD-6 ("single issuer and verifier, no third-party verifiers; revisit with RS256/ES256 if any other service validates tokens") and require `ValidAlgorithms = [HS256]`, `ValidateIssuerSigningKey`, and an `IssuerSigningKeyResolver` for `kid`, with `MapInboundClaims = false`.

### Medium
- **M1 - Token minting library unnamed/unpinned (AD-6).** JwtBearer only validates. Creating tokens needs `Microsoft.IdentityModel.JsonWebTokens` (or `System.IdentityModel.Tokens.Jwt`), transitive but version-sensitive, not in the Stack table. Fix: add the row (use `JsonWebTokenHandler`, current with JwtBearer 10).
- **M2 - Konscious has had no release for about 28 months (AD-8).** Still the widest-used managed Argon2id; no better maintained option confirmed. Fix: note maintenance state in AD-8 and hide it behind `IPasswordHasher` (the PHC string is already self-built, so swapping is cheap); re-check before release.
- **M3 - SDK pin looks stale.** "local SDK 10.0.103" is an early-2026 SDK while runtime/ASP.NET patches are at 10.0.12 (inferred; latest SDK band not directly confirmed). Fix: `global.json` with `rollForward: latestPatch` and a current SDK; state "10.0.x latest patch" as the rule rather than 10.0.103.
- **M4 - xunit.v3 "4.0.x" is loose.** 4.0.x is only about 2 months old and had 3 patches. Fix: pin 4.0.2 in `Directory.Packages.props` (and the matching `xunit.v3.mtp-v2`/runner packages if using Microsoft.Testing.Platform; not verified).
- **M5 - Build-time OpenAPI needs an unnamed package (AD-18).** "Emitted as a build artifact" requires `Microsoft.Extensions.ApiDescription.Server`. Fix: add to Stack; also note the host project must run the app-startup path at build, so build-time generation must not trigger `ValidateOnStart` secrets or DB access.

### Low
- **L1** Testcontainers.PostgreSql: 4.15.0 verified; 4.16.0 may exist. Fix: bump if on NuGet at scaffold.
- **L2** .NET 10 end-of-support date differs between sources (10 vs 14 Nov 2028). Fix: write "November 2028".
- **L3** PostgreSQL "18.6 at authoring" is correct (2026-08-13); fine, but say "latest 18.x minor" to avoid staleness.
- **L4** Not pinned in Stack although named: Docker image tag (`postgres:18`), Microsoft.Extensions.ApiDescription.Server, token library (M1), architecture-test library (already deferred), validator library if FluentValidation is intended ("Validator" slice file implies one; unnamed), `EFCore.NamingConventions` or equivalent for snake_case (AD-17, unnamed, check it supports EF 10 or map names manually). Fix: add rows or state "hand-written".
- **L5** TimeZoneInfo: AD-15 `ValidateOnStart` of "valid IANA id" should note tzdata must be in the runtime container image and invariant globalization must stay off; macOS/Linux dev fine. Fix: add a startup test and a Dockerfile note.

## Starter check (greenfield)
Spine leans on `dotnet new sln` producing .slnx on .NET 10; memlog says verified. Not re-verified here (needs local SDK; consistent with .NET 10 docs). Status: unconfirmed in this review, low risk.

## Memlog note
The memlog records versions but no source URLs or dates for any of them, and "(question) architecture test library not verified" is correctly deferred. Adding source and date per version would let a later reader judge staleness.
