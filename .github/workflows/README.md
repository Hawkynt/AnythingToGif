# CI/CD Pipeline — AnythingToGif

> Everything in this folder is the automated pipeline for this repository.
> Workflows live here, their helper scripts live in `scripts/`.

## What this does

Three workflows, one shared build block, three helper scripts:

| File                            | Trigger                             | Purpose                                 |
|---------------------------------|-------------------------------------|-----------------------------------------|
| `ci.yml`                        | push + PR + `workflow_call`         | Build & test on every change            |
| `release.yml`                   | tag push `v*` + manual dispatch     | Cut a GitHub Release + push to NuGet    |
| `nightly.yml`                   | successful CI run on `master`/`main`| Publish `nightly-YYYY-MM-DD` prerelease |
| `_build.yml`                    | `workflow_call` (internal)          | Shared CLI-zip + NuGet-pack block       |
| `scripts/version.pl`            | invoked by the workflows            | Compute `X.Y.Z.BUILD`                   |
| `scripts/update-changelog.mjs`  | invoked by the workflows            | Bucketise commits into CHANGELOG.md     |
| `scripts/prune-nightlies.mjs`   | invoked by the workflows            | 3-gen (GFS) retention of nightlies      |

## How it works

```
                push / PR
                    │
                    ▼
            ┌───────────────┐
            │    ci.yml     │──► test + coverage on windows-latest
            └───┬───────┬───┘
                │       │
    tag v* ─────┤       │  on success on master/main
                ▼       ▼
        ┌──────────┐  ┌─────────────┐
        │ release  │  │  nightly    │
        │  .yml    │  │   .yml      │
        └────┬─────┘  └─────┬───────┘
             │              │
             ▼              ▼
        (both call _build.yml)
             │              │
             ▼              ▼
     GH Release v1.2.3   nightly-YYYY-MM-DD (prerelease)
     + Hawkynt.GifFileFormat.nupkg   (CLI zip only, no nupkg)
       on nuget.org                        │
                                            ▼
                                 scripts/prune-nightlies.mjs
                                 (GFS: 7 daily + 4 weekly + 3 monthly)
```

## What it's for

- Every PR is built and tested on windows-latest before it can merge.
- Every merge to `master`/`main` produces a **tested** nightly prerelease that pins to the exact SHA CI validated.
- Every `v*` tag cuts a proper release **and** pushes `Hawkynt.GifFileFormat` to nuget.org.
- Old nightlies are auto-pruned on a **Grandfather-Father-Son** schedule — 7 daily + 4 weekly + 3 monthly.

## Why it's built this way

- **No cron triggers.** The old `Build.yml` ran weekly and pushed NuGet packages from every scheduled build — silently shipping from unreviewed code. This pipeline only fires on actual events, and NuGet push is **tag-gated**.
- **Windows-only runner.** `AnythingToGif` targets `net8.0-windows` with FFmpeg native DLLs. Tests can't execute on Linux runners even with `EnableWindowsTargeting`, so CI stays on `windows-latest`.
- **Nightly CLI only, no NuGet.** We don't pollute nuget.org with prereleases. If you need a pre-tag build of `Hawkynt.GifFileFormat`, consume it from the corresponding nightly GitHub release.
- **Release calls CI via `workflow_call`.** Tag pushes don't retrigger `on: push` workflows, so this pipeline invokes the same test matrix explicitly.
- **Nightly builds from the `workflow_run` payload's SHA**, not branch tip — so a nightly is always a build of code CI actually validated.
- **`_build.yml` is shared**, not duplicated between `release.yml` and `nightly.yml`.
- **3-generation (GFS) retention**, not "keep last N". GFS guarantees at least one build per week for a month and one per month for a quarter.

## Scripts

### `version.pl`

Reads `<Version>X.Y.Z</Version>` from the first csproj found at root or one level deep. Build number is `git rev-list --count HEAD`.

```
perl .github/workflows/scripts/version.pl          # 1.0.2.114
perl .github/workflows/scripts/version.pl --base   # 1.0.2
perl .github/workflows/scripts/version.pl --build  # 114
perl .github/workflows/scripts/version.pl --stamp  # writes X.Y.Z.BUILD into every csproj
```

Replaces the old `UpdateVersions.pl`.

### `update-changelog.mjs`

Prepends a new section to `CHANGELOG.md`. Commit-subject convention: `+` Added, `*` Changed, `#` Fixed, `-` Removed, `!` TODO, anything else → Other.

### `prune-nightlies.mjs`

GFS retention with `DAILY_KEEP=7`, `WEEKLY_KEEP=4`, `MONTHLY_KEEP=3`. Dry-run with `--dry-run`.

## Who maintains this

Every repo in the CompressionWorkbench / PNGCrushCS / AnythingToGif / ClaudeCodePortable family owns its own copy. When changing it, prototype here then mirror the change to the siblings.

## Release artifacts

| Artifact                                                   | Produced by          | Destination                         |
|------------------------------------------------------------|----------------------|-------------------------------------|
| `AnythingToGif-cli-win-x64-<version>.zip`                  | release + nightly    | GitHub Release                      |
| `AnythingToGif-AlgorithmComparison-win-x64-<version>.zip`  | release + nightly    | GitHub Release                      |
| `Hawkynt.GifFileFormat.<version>.nupkg`                    | release only         | GitHub Release + nuget.org          |
