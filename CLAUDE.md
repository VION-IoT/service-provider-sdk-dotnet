> **Cross-repo work**: this repo is part of the VION platform.
> Architecture state, decisions, and cross-repo specs live in [`../architecture`](https://github.com/VION-IoT/architecture).
> Clone it: `git clone git@github.com:VION-IoT/architecture.git ../architecture`
> Before planning a feature with scope ≥ 2 repos, read the relevant `architecture/systems/*.md`
> and run `/spec <slug> <repos>` from the architecture repo.
> Cross-repo work is dispatched with the `vion-dispatch` plugin
> ([mechanics](https://github.com/VION-IoT/architecture/blob/main/plugins/vion-dispatch/README.md),
> [VION procedure](https://github.com/VION-IoT/architecture/blob/main/runbooks/session-orchestration.md)).
> A session dispatched into this repo ends with `/vion-dispatch:report`.

# CLAUDE.md — service-provider-sdk-dotnet

**Optional** C# convenience for authoring on-edge service providers. The
SP↔mesh wire is owned by [`Vion.Contracts`](https://github.com/vion-iot/vion-contracts);
non-C# SPs (Python, Structured Text, anything that speaks MQTT)
implement the protocol directly. This SDK exists so C# SP authors don't
have to hand-write the registration handshake, declaration workflow, or
log-level / restart wiring.

Targets `net10.0`. The choice is driven by `MQTTnet`'s own targets — if
the upstream MQTT client adds a netstandard build, we'd happily follow.

## The shape consumers compile against

Reference shape from
[`hal-raspberry/Program.cs`](https://github.com/vion-iot/hal-raspberry):

```csharp
var secret = await LoadOrCreateSecretAsync(secretFilePath);
var config = new ServiceProviderClientConfigurationBuilder(mqttConnectionData, secret, RegistrationCredentials.WellKnown)
    .WithDeclaration(setup.CreateDeclarationCallback)
    .WithHandlers(setup.CreateHandlers)
    .WithRestartCallback(/* ... */)
    .WithLogLevelChangeCallback(/* ... */)
    .Build();
var client = new ServiceProviderClient(config, new MqttClientFactory(), logger);
await client.StartAsync(stoppingToken);
```

Three inputs the SP author always provides: `mqttConnectionData` (broker
host / port / TLS), `secret` (the **pairing secret** — typically
loaded from `data/secret.txt`, created on first run), and
`RegistrationCredentials.WellKnown` (from `Vion.Contracts.Mqtt`) — the fixed,
public credentials the registration connection authenticates with. The SDK uses them
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

## Load-bearing constraints

- **The package is public.** It ships to nuget.org for SP authors outside VION, so a pull request
  that breaks a consumer says so in its description, and the release that ships it is a major.
  [`docs/releasing.md`](docs/releasing.md) says what breaks one.

## Read before you write

Read the linked doc before doing the matching work, and follow it.

| When you're… | Read |
| --- | --- |
| writing or changing C# code | [`docs/conventions/code-style.md`](docs/conventions/code-style.md) |
| writing or changing a test | [`docs/conventions/testing.md`](docs/conventions/testing.md) |
| writing or reworking a comment or XML documentation | [`docs/conventions/comment.md`](docs/conventions/comment.md) |
| cutting a release | [`docs/releasing.md`](docs/releasing.md) |

## Working agreement

### Lanes

At the start of a task, answer two questions out loud: is the change local? is a design point open?

- **Fix-sized** — local, nothing open: branch, commit, review, pull request. No document. A change
  that turns out not to be local stops and says so: it is feature-sized.
- **Feature-sized** — a change doc first, in `docs/changes/`. Ratified before code when a question
  in it is open. Archived in the pull request that lands it.

### STOPs

- A STOP is named up front — by the brief, an open question in the change doc, or the lane answer —
  and no other. With none named, human review is on the pull request.
- A STOP is a `partial` REPORT with a question in it.
- A decision nobody named is surfaced, not taken. A hedge in a brief is a STOP when it fails.
- Scope does not widen on its own: a design or naming question gets options and changes nothing
  until the human chooses; work nobody asked for is proposed, not produced.
- A question from the human is a question, not an instruction.
- Anything committed after a `done` REPORT needs a new REPORT.
- A request that breaks a convention of this repo is pushed back on before complying, by name.
- Verification only a human can do is not a STOP: write it as "not run, routes to a human" under the
  pull request's Verification.

### Communication

- Say what was run, not that it worked.
- A claim a decision rests on names its evidence: a command, a file and line, or that it is inferred.
- Promise no notification that cannot be subscribed to.

### Never

- Push to or commit on the default branch.
- Force-push.
- Delete a remote branch.
- Merge a pull request.
- Write to Jira without saying so first.
- Paste a secret into chat.

## Skills in this repo

| moment | skill |
|---|---|
| starting work on a change | `/vion-git:branch` |
| a unit of work lands — a task, a criterion, a fixed review finding | `/vion-git:commit` |
| a correction to produced work, tooling that fought or false-passed, upstream that was wrong, a settled point, a grumble | `/vion-improve:journal` |
| editing a file written for the agent — `CLAUDE.md`, a command, a skill, a convention doc, settings | `/vion-improve:harness` |
| the branch is ready for a pull request | `/vion-git:pr` |
| a retro is due, by the count and age `retro` states | `/vion-improve:retro` |

### Pre-PR obligations

1. On `*.cs`, `*.csproj`, `Directory.Build.*`, `.editorconfig`, `Vion.ServiceProvider.Sdk.sln.DotSettings`,
   `.config/dotnet-tools.json`, `scripts/cleanup-code.ps1`: `pwsh scripts/cleanup-code.ps1`
   (`/cleanup`); commit what it changes.
2. On `*.cs`, `*.csproj`, `Directory.Build.*`, `.editorconfig`, `Vion.ServiceProvider.Sdk.sln`:
   `dotnet build Vion.ServiceProvider.Sdk.sln`, then `dotnet test Vion.ServiceProvider.Sdk.sln`.

### Reader depth

Beyond `/vion-git:pr`'s defaults:

- harness: `docs/conventions/**`

### Parallel sessions

The main checkout stays on `main`; a branch lives in `../service-provider-sdk-dotnet-<key>`
(`/vion-git:branch`).

A new worktree gets nothing copied into it, so it lacks the main checkout's ignored local state:
`.idea/`, IDE state that build, test and cleanup do not read. No port is a singleton: build, test
and cleanup write only inside the checkout they run in.

## How this file stays true

Harness budget, in bytes of the committed file: this file 10 kB. No gate reads this number —
enforced by hand.
