# Releasing

Maintainer reference. Not for public consumption — see the root [README](../README.md) for the user-facing intro.

## How versions work

Git tags drive versions. There is no `<Version>` in `Vion.ServiceProvider.Sdk.csproj` — `Directory.Build.props` provides a `0.0.0-local` fallback.

| Trigger | Published version | Destination |
|---|---|---|
| Push to `main` | `0.0.0-ci.{run_number}` | Private Azure DevOps feed only — for internal integration testing, never depend on from shipped code |
| Push tag `v0.1.0` | `0.1.0` | Private feed + nuget.org |
| Push tag `v0.2.0-preview.1` | `0.2.0-preview.1` | Private feed + nuget.org (treated as pre-release) |

## Cutting a release

Prerequisites:
- `main` is green on the commit you want to release.
- `gh` is installed and authenticated (`gh auth status`).

```bash
# Stable:
gh release create v0.1.0 --target main --generate-notes \
  --title "v0.1.0" --notes "Short release summary."

# Pre-release (add --prerelease for the UI badge; NuGet detects pre-release
# automatically from the SemVer suffix):
gh release create v0.2.0-preview.1 --target main --prerelease --generate-notes \
  --title "v0.2.0-preview.1" --notes "What this preview validates."
```

`gh release create` creates the git tag (at the `--target` commit) and the GitHub Release in one step. The new tag triggers [`publish.yml`](../.github/workflows/publish.yml):

1. Builds and packs with `Version` taken from the tag (strips the `v` prefix).
2. Pushes `.nupkg` + `.snupkg` to the private Azure DevOps feed.
3. Publishes to nuget.org using [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (short-lived OIDC token — no API key stored).

Verify the result under the [VION-IoT profile on nuget.org](https://www.nuget.org/profiles/VION-IoT).

## Version immutability

Once a version is published to nuget.org, the version ID is permanent. You can *unlist* a version (which hides it from search and `dotnet add package`), but the ID stays burned — you cannot re-upload the same version, even after yanking. Pick the next number for any subsequent change, even a tiny fix.

## Required configuration

One-time setup per repo. Flag this if you fork or rotate credentials:

- GitHub secret `AZURE_DEVOPS_PAT` — PAT with `Packaging: Read & write` on the Azure DevOps feed.
- GitHub secret `NUGET_USER` — **individual** nuget.org username that is a member of the org owning the Trusted Publishing policy. Not the org name. (Docs use `contoso-bot` as the example — this matters; setting it to the org name produces `No matching trust policy owned by user 'X' was found`.)
- Trusted Publishing policy on nuget.org: Package Owner `VION-IoT`, Repository Owner `VION-IoT`, Repository `service-provider-sdk-dotnet`, Workflow File `publish.yml`. See [NuGet's Trusted Publishing docs](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) for the UI walkthrough.
