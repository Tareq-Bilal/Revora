# IdentityService Farming — Design

## Goal

Remove dead/unused files, code, and config from `src/IdentityService`
without changing runtime behavior.

## Scope

Out of scope, explicitly deferred by decision:

- `keys/is-signing-key-*.json` — auto-generated Duende signing key, tracked
  in git. Live key material, not dead code. Removing it forces IdentityServer
  to mint a new key on next startup and invalidates tokens signed with the
  old one — functional impact, so excluded from this cleanup.
- `buildschema.bat` / `buildschema.sh` — dev scripts to regenerate EF
  migrations. Still in active use. Keep both.
- `DESIGN-bmw-m.md` — canonical UI design-tokens doc, not cruft. Keep.

Audited and confirmed clean, no action needed:

- `IdentityService.csproj` package references — all referenced and used,
  no Sqlite package present (confirms nothing depends on the SQLite file
  below).
- `appsettings.json` — no orphaned configuration keys.
- Comments in `HostingExtensions.cs` — live Duende-documentation comments,
  not dead code blocks.

## Change

Remove `src/IdentityService/AspIdUsers.db` (tracked, ~106KB).

**Why it's dead:** `ApplicationDbContext` is registered with
`options.UseNpgsql(...)` in `HostingExtensions.cs:75`. Postgres is the only
configured provider; no Sqlite EF provider package exists in the project.
The file is a leftover artifact from before the project moved to Postgres
and nothing in the codebase opens or references it.

**Verification before removal:** confirm no `UseSqlite` call and no
reference to the filename anywhere in `src/IdentityService` (already
checked — none found).

## Implementation

1. `git rm src/IdentityService/AspIdUsers.db`
2. Re-run IdentityService (`dotnet watch` from `src/IdentityService`) to
   confirm clean startup with Postgres, unaffected by the removal.

## Out of scope for this spec

The OAuth guide for junior engineers is a separate, independent
deliverable — tracked as its own spec.
