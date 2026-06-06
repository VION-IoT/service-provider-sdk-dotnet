> **Cross-repo work**: this repo is part of the VION platform.
> Architecture state, decisions, and cross-repo specs live in [`../architecture`](https://github.com/VION-IoT/architecture).
> Clone it: `git clone git@github.com:VION-IoT/architecture.git ../architecture`
> Before planning a feature with scope ≥ 2 repos, read the relevant `architecture/systems/*.md`
> and run `/spec <slug> <repos>` from the architecture repo.

# CLAUDE.md — service-provider-sdk-dotnet

**Optional** C# convenience for authoring on-edge service providers. The
SP↔mesh wire is owned by [`Vion.Contracts`](https://github.com/vion-iot/vion-contracts);
non-C# SPs (Python, Structured Text, anything that speaks MQTT)
implement the protocol directly. This SDK exists so C# SP authors don't
have to hand-write the registration handshake, declaration workflow, or
log-level / restart wiring.

See [`architecture/libraries/service-provider-sdk-dotnet.md`](../architecture/libraries/service-provider-sdk-dotnet.md)
for the cross-repo view and [`architecture/concepts/service-providers.md`](../architecture/concepts/service-providers.md)
for what an SP is.

## Build / test

```powershell
dotnet build Vion.ServiceProvider.Sdk.sln
dotnet test Vion.ServiceProvider.Sdk.sln
```

Targets `net10.0`. The choice is driven by `MQTTnet`'s own targets — if
the upstream MQTT client adds a netstandard build, we'd happily follow.

## Code Style

- C# with `ImplicitUsings: false` (all usings explicit).
- `Nullable: enabled`.
- Code cleanup: **ReSharper `cleanupcode`** with the `Custom: Full Cleanup (excl. optimize usings)` profile (JetBrains CLI) — see the cleanup note below. Do NOT use the `Built-in: Reformat Code` profile.
- Allman brace style throughout.
- Targets `net10.0`.

Code style is **ReSharper cleanupcode** with the `Custom: Full Cleanup (excl. optimize usings)`
profile in `Vion.ServiceProvider.Sdk.sln.DotSettings` — the same profile ReSharper/Rider apply on
save. The single source of truth is **`scripts/cleanup-code.ps1`**: it restores the pinned `jb`
tool (`.config/dotnet-tools.json`) and runs the exact cleanup. CI runs the same script with
`-Verify` (fails on drift) via the shared `VION-IoT/shared-workflows` gate:
`.github/workflows/publish.yml` calls `publish-nuget.yml` with `gate: true`, which runs
`scripts/cleanup-code.ps1 -Verify` (the `dotnet-gate` composite) before packing — so local and CI
can't diverge.

**Before opening a PR: run `pwsh scripts/cleanup-code.ps1` (or the `/cleanup` slash command),
review `git diff`, and commit any changes** — this keeps the CI style gate from failing the PR.
**Agents: do this automatically before `gh pr create`.** Do NOT run cleanup with
`--profile="Built-in: Reformat Code"` — it differs from the DotSettings profile and fights
cleanup-on-save.

## Where stuff lives

| Path | Holds |
|------|-------|
| `Vion.ServiceProvider.Sdk/RegistrationFlow/` | The main API: `ServiceProviderClient`, `ServiceProviderClientConfigurationBuilder`, the `IServiceProviderClient` / `*Handler` / `*Publish` interfaces, `ServiceProviderTopics`, payload types |
| `Vion.ServiceProvider.Sdk/JsonSerializationContexts/` | `System.Text.Json` source-generated contexts for SP↔mesh payloads |
| `Vion.ServiceProvider.Sdk/Tracing/` | OpenTelemetry tracing helpers for SP-side spans |
| `docs/` | Maintainer notes ([`releasing.md`](docs/releasing.md), [`package-readme.md`](docs/package-readme.md)) |

## The shape consumers compile against

Reference shape from
[`hal-raspberry/Program.cs`](https://github.com/vion-iot/hal-raspberry):

```csharp
var secret = await LoadOrCreateSecretAsync(secretFilePath);
var config = new ServiceProviderClientConfigurationBuilder(mqttConnectionData, secret)
    .WithDeclaration(setup.CreateDeclarationCallback)
    .WithHandlers(setup.CreateHandlers)
    .WithRestartCallback(/* ... */)
    .WithLogLevelChangeCallback(/* ... */)
    .Build();
var client = new ServiceProviderClient(config, new MqttClientFactory(), logger);
await client.StartAsync(stoppingToken);
```

Two inputs the SP author always provides: `mqttConnectionData` (broker
host / port / TLS) and `secret` (the **pairing secret** — typically
loaded from `data/secret.txt`, created on first run). The SDK uses both
only to complete registration; mesh issues operational nanomq
credentials on acceptance, and the SDK reconnects with those.

## Adding builder hooks

`ServiceProviderClientConfigurationBuilder` is the public extensibility
point. When adding a new hook (e.g. a new lifecycle callback that mesh
exposes via the SP↔mesh protocol):

- Land the corresponding payload type and topic constant in
  [`Vion.Contracts`](https://github.com/vion-iot/vion-contracts) first
  — this SDK should never define wire-level types.
- Add a `.With…` method on the builder that takes a delegate; default
  to a no-op handler if not supplied.
- Wire the dispatch inside `ServiceProviderClient`'s message-received
  pipeline; don't add a parallel client instance.
- If the hook produces a payload that's serialized, regenerate the
  `JsonSerializationContexts/` source-gen output and commit it.

## What this SDK is *not*

- **Not a wire-format definition.** All payload classes, MQTT topic
  constants, and user-property names live in
  [`Vion.Contracts`](https://github.com/vion-iot/vion-contracts).
  Adding a wire type here would create a hidden contract that non-C# SPs
  can't see.
- **Not a logic-block authoring SDK.** That's
  [`vion-iot/dale-sdk`](https://github.com/vion-iot/dale-sdk) — it lives
  on the consumer side (inside `dale`), not the SP side.
- **Not opinionated about the SP's hosting.** Bring your own
  `IHostBuilder`, lifetime, logging, tracing setup. The SDK plugs into
  whatever you have.

## Versioning & releases

Tag-driven: `v0.1.0` → publishes `0.1.0` to nuget.org and the private
Azure DevOps feed. Pushes to `main` publish `0.0.0-ci.<run>` to the
**private feed only** — never depend on those from shipped code.
Versions on nuget.org are immutable.

Canonical reference: [`docs/releasing.md`](docs/releasing.md) +
[`publish.yml`](.github/workflows/publish.yml).

When a major bump in [`Vion.Contracts`](https://github.com/vion-iot/vion-contracts)
introduces breaking schema changes, this SDK's version moves in
lockstep — consumers expect a matched `Vion.Contracts` /
`Vion.ServiceProvider.Sdk` pair.

## Source availability

Apache-2.0; source-available. PRs from outside `vion-iot` are not
accepted — see [`CONTRIBUTING.md`](CONTRIBUTING.md). The package is
public on nuget.org so external SP authors can consume it.
