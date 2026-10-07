# Testing conventions

§1 holds above every other rule; §2 is this repo's test project; §§3–11 are the authoring discipline
every test follows.

## 1. Test integrity (critical)

Tests exist to find bugs. When a test fails:

- **Do not** delete or skip the failing test to keep the suite green.
- **Do not** rewrite the test to match the actual (buggy) behavior.
- **Do not** loosen an assertion just enough for it to pass.

Tests must be written against the *expected* behavior. If a test fails and the production code looks wrong, stop and surface the suspected bug rather than mutating the test.

## 2. Test project setup

A test assembly is named `<Project>.Test`. `Directory.Build.props` sets only the version, package
metadata and Source Link, so the csproj sets its own target framework, language version and
nullability:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <LangVersion>latest</LangVersion>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="..."/>
        <PackageReference Include="MSTest" Version="..."/>
        <PackageReference Include="Moq" Version="..."/>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Vion.ServiceProvider.Sdk\Vion.ServiceProvider.Sdk.csproj"/>
    </ItemGroup>

    <ItemGroup>
        <Using Include="Microsoft.VisualStudio.TestTools.UnitTesting"/>
    </ItemGroup>

</Project>
```

- Use the **latest stable** versions of `Microsoft.NET.Test.Sdk` and `MSTest`; add `Moq` only when a
  test in the assembly mocks.
- Use the `MSTest` metapackage — not the separate `MSTest.TestAdapter` + `MSTest.TestFramework`
  references.
- Do **not** set `ImplicitUsings`: every `using` is explicit
  ([`code-style.md`](code-style.md)), and the `<Using>` item above is the one global using.
- `IsPackable` is `false`, which keeps the assembly out of the pack and out of
  `Directory.Build.targets`' shared package README.
- No `coverlet.collector`: nothing collects coverage.
- Add the project to `Vion.ServiceProvider.Sdk.sln`; `dotnet test Vion.ServiceProvider.Sdk.sln` and CI
  run only what the solution lists.

## 3. Coverage — every observable behavior, not every line

The discriminator is **observability**: a behavior is something a caller, collaborator, subscriber,
or listener can detect — a distinct return value, a guard that changes
the outcome, conditional construction, a raised event, a side effect on a collaborator, a resilience
loop. A branch that changes nothing observable (one that only picks a different log message) is not
a behavior and needs no test. Walk the SUT **statement by statement, not branch by branch** — a
linear method calling four collaborators in order is doing four things, each independently
deletable. The commonly missed cases, all observable, all worth a test:

- **A sequence of side effects is a sequence of behaviors** — ask of each call whether deleting it
  changes something a collaborator can see.
- **Verify the constructed object, not just that the call happened.** `Verify(c => c.Send(It.IsAny<Foo>()))`
  proves a call occurred, not that the fields were populated — content assertion per §10.
- **Exception branches that change the outcome** (different return, skipped side effect,
  swallowed-vs-propagated) — but don't fabricate exotic failures for a `catch` that only logs.
- **Both sides of a conditional construction** — a provided test asserting the value is used *and*
  a not-provided test asserting the fallback.
- **Loop / multi-subscriber resilience** — register a throwing subscriber before a recording one
  and assert the recording one still ran.
- **A side effect that survives a branch** — when the SUT branches and then acts unconditionally,
  a row on *each* side of the branch pins that the effect stays unconditional.

Don't chase line or branch percentages, and don't assert negatives the language already guarantees
(exception unwinding reaching the next statement). A deliberate *decision* not to catch earns an
explicit throws-test exactly when "just catch and ignore it" is a plausible future fix.

**A test deleted because "another gate already covers this" names the gate and its failure mode** in
the commit. An analyzer rule at suggestion severity, for example, fails no build.

## 4. Enumerate the behaviors before writing the tests

Read the SUT and produce a table of its observable behaviors and the test that will cover each —
**in the response, before any test code**:

| Behavior | Test |
|---|---|
| Not mapped → drive refused, value unchanged | `RefuseDriveWhenUnmapped` |
| Response without a matching request → ignored | **none — unobservable, say why** |

The table is the reviewable artifact: a reader disagrees with a row in seconds; finding the same
gap by reading fifty test bodies takes an hour. **Rows with no test are the point** — list them
with the reason (unobservable, covered elsewhere) rather than omitting them. Derive rows from the
SUT, never from the tests already written. Produce it whenever the request is
about *coverage* (a new `…Should` class, "write tests for this", a rewrite); skip it when the
request names the behavior ("add a test for the timeout path").

## 5. A test must be able to fail — the vacuous-test catalogue

If deleting the Arrange leaves the assertion passing, the test asserts what the Act alone produces.
The recurring shapes:

- **Idempotency / cache hit**: capture the first result and assert the second equals *it* — not a
  literal a fresh call also returns.
- **Reset / clear**: make the pre-state observably different from the post-state first, or the
  assertion reads the same on an untouched SUT.
- **In-place mutation**: when the SUT populates an object and hands it to a collaborator, assert
  through what the collaborator *received* (Callback capture), not through the test's own reference —
  the direct assert passes with the handoff deleted.
- **Substring on a rendering**: `StringAssert.Contains(detail, "0 virtual s")` passes on
  `"60 virtual s"` — the string it excludes contains the string it requires. Where a test
  discriminates two renderings of one field, assert the field whole.
- **The assertion is whatever would actually fail.** Where the outcome is only observable as "it
  reached a terminal state", the awaited signal *is* the assert — put it under `// Assert` and do
  not follow it with a `Verify` the await already guarantees.
- **The fixture has to be able to carry the observable.** A source no listener samples cannot show
  a span's tags, whatever the assertion says. Find the seam or the fixture that shows it *before*
  writing the test.

**Prove every new behavioral test red**: revert the fix or delete the
branch, watch it fail, restore — and **name the mutation in the PR** ("made the replay conditional →
`PublishAllStatesToLateSubscriber` fails"). A red-proof belongs to the assertion it ran against;
re-prove after rewriting it.

**A test that pins an ordering, a bound or an edge value runs its own mutation once before it is
cited.** A test that survives its mutation pins nothing.

**A red run is read for the assertion that failed.** A test that fails on its own reddens under every
mutation, so a red summary line proves nothing until the failure message is the claim's own and the same
test is green without the mutation. A runner that filters by `Name~` matches a `[DataRow]` test by its
`DisplayName`, not its method, and runs nothing — filter by `FullyQualifiedName~`. A mutation that does not
compile runs nothing too, and an output filter that keeps only test lines hides why, so read the build's own
result before reading a mutation run's absence of a red line. Restore a mutation from a copy of the
file taken before it, or by its exact reverse edit — never `git checkout`, which also discards the
uncommitted work that file carries. Restoring the source does not restore the build: rebuild before any
later `--no-build` run, or it runs the mutated binaries and reads as a regression.

**A criterion that decides between two readings is tested where they part.** When a thing is recorded,
what a bound covers, which of two orders wins: a test on the path where both readings give the same
observable passes under either, and its mutation proves the mechanism exists, not that it is the right
one. Build the case where the wrong reading gives a different observable.

**A `[DataRow]` merge is a rewrite of the assertion**: re-derive the mutation after it, or the merge
is a deletion.

**A mutation that survives is a hypothesis about the test as much as about the mutation.** When
another mechanism produces the same observable, remove *that mechanism* from the fixture rather than
strengthen the mutation.

**Settle step-versus-field with the mutation.** Fields are the arguments of one call; steps are
calls in a sequence. Mutate once per candidate assertion: assertions that redden under *different*
mutations are different behaviors and belong in different tests; assertions that can only redden
together are one behavior in one test.

## 6. Reach for a seam, never around the SUT

- **No reflection** to reach private or internal members from a test — no `BindingFlags.NonPublic`,
  no `FieldInfo.SetValue`, no `MethodInfo.Invoke` on a private method.
- **No test-only accessors** — never add a member to the SUT whose sole purpose is letting tests
  inspect internal state. It widens a published package's surface for non-production reasons and
  couples tests to the current representation. Observe the behavior instead: a return value, a
  published message, an event, or a side effect on a collaborator.
- **Heavy setup, deeply nested mocks, or several unrelated things verified at once** signal that the
  SUT is probably doing too much. Hard-to-test code is usually hard-to-reason-about code; a
  torturous test locks the design in.

In each case, stop and ask rather than push through: describe what the test is for, what blocks it,
and the options seen — extract a class, widen visibility via `InternalsVisibleTo`, change the SUT's
API, split the SUT. Do not refactor production code unilaterally to make a test work.

## 7. Naming

Form a sentence with the class name — `[Sut]Should` + `[ExpectedResult][Condition]`:
`DeliverWriteIssuedFromStopping`, `ThrowWhenPayloadEmpty`, `RefuseDriveWhenUnmapped`.

- **Name the behavior, not the collaborator** — `ReturnStoredTopology` ✓, `ReturnTopologyFromRepositoryProvider` ✗.
  Exception: routing SUTs, where the destination *is* the behavior.
- **Name the condition, not the exception type** — `ThrowWhenPortInUse` ✓; the throws-assert pins
  the type.
- **No specifics that churn** — no counts (`ListAllSevenFields` ✗), no format details, no magic
  numbers; the assertion owns the specifics.
- **Outcome, not mechanism** — `PublishLatestValue` ✓, `CollapseDuplicateKeys` ✗.
- **No articles or filler** — drop `The`, `A`, `Is`: `ThrowWhenPayloadEmpty`, never
  `ThrowWhenThePayloadIsEmpty`.
- **Numeric suffixes for same-role peers** — `_matchingHandler1Mock`, `serviceIdentifier2` — never
  position or novelty words (`first…`, `other…`), which don't scale past two.
- **`expected`/`actual` are prefixes on a meaningful base noun** (`expectedPayload`), used only when
  both sides of a comparison are variables; a result asserted against a literal gets a plain name.

## 8. Structure — Triple-A and its discipline

Every test carries `// Arrange`, `// Act`, `// Assert` — always, even when Arrange is empty.
Discipline:

- `// Act` holds only the interaction with the SUT. A conversion applied solely for comparability
  (`.ToString()`, `.ToList()`) belongs in `// Assert`; an awaited settlement of the SUT's async work
  belongs in `// Act`. When Act and Assert can't separate, `// Act / Assert` with MSTest's
  `Assert.Throws*` — never `[ExpectedException]`,
  never `try/catch`, in Arrange included (consume an arranged failure with the throws-assert).
- **Declare locals next to their use**, ordered by when each value is needed — not front-loaded by
  category.
- **Inline expected values in the assertion.** Bind an `expected…` local only when a second reader
  needs it, something before the assert consumes it, or an `It.Is` predicate would bury it (§10).
- **Helpers earn their keep**: extract for duplication across multiple call sites or genuinely
  gnarly setup — never a single-use helper, never a wrapper around a one-line `Setup`/`Verify`, and
  the call to the SUT always stays inline in the test's own `// Act`. When in doubt, inline.

### File layout and ordering

One test file per SUT class, named and classed `[Sut]Should`, its path mirroring the source path
(`Vion.ServiceProvider.Sdk/SystemControl/LogLevelStore.cs` →
`Vion.ServiceProvider.Sdk.Test/SystemControl/LogLevelStoreShould.cs`). Order
tests to mirror the SUT's execution flow so a reviewer can scan SUT and tests top-down in parallel:
member order across public members; within a method, its branch order (guards first, then the main
path); for a linear method, happy path first, then variations. Ordering is organizational, not a
coverage lever. Shared helpers (builders, fakes used by several test classes) live in a
`TestHelpers/` folder at the test project root; single-class helpers stay private to the class.

### Setup

Initialise mocks and the SUT inline at field declaration when possible (`_sut`, `_loggerMock`);
otherwise `[TestInitialize]` with `_sut = null!` declarations. Only truly
common setup goes there — anything that varies belongs in the test's Arrange. Prefer uniform
construction over per-test SUT variation: vary the test's *inputs*, not the constructor args. For a
SUT needing a baseline state (an established connection), arrange it in setup and
`_mock.Invocations.Clear()` so tests observe only what they triggered. Default to **no**
`[TestCleanup]`/`Dispose` — add one only to fix a failure you can name.

### Parameterised rows

`[DataRow]` rows are **value variations of one condition** — the method name stays
the assertion, the rows are instances. Rows must share one Arrange shape: a parameter selects
values, never structure — an `if` on the parameter means the rows are different scenarios, so
split them. Passthrough/transformation tests take **at least two distinct value sets** (one fixed
input passes spuriously if the SUT hardcodes it). Add `DisplayName` when the
raw values don't identify the row. Object-typed rows use `[DynamicData]`
backed by a static member. Don't lump unrelated scenarios into one parameterised
test, and collapse a with-X/without-X pair into one row pair rather than a separate `Omit…` test.

## 9. Test data

- **Don't supply values that aren't asserted on.** Drop the parameter where you control the surface;
  pass `null`/`default` where you don't. `new InvalidOperationException()` unless a test reads the
  message.
- **`Guid.NewGuid().ToString()` for strings the SUT requires but the test doesn't care about** — it
  signals "any value works" where `"localhost"` makes a reader hunt for significance. Literal
  carve-outs only for attribute rows (compile-time constants), asserted values, and numerics.
- **`CancellationToken.None` when the token is plumbing** — verify with `It.IsAny<CancellationToken>()`;
  a real token only where the SUT's own logic branches on it.
- **Never fabricate calendar literals.** `DateTime.UtcNow` captured once into a field.
- **Class level only when shared** by multiple tests; single-use values live in the test's Arrange.

## 10. Moq

- `new Mock<X>()` then `.Object` — not `Mock.Of<X>()`. Mock loggers; never assert on log calls
  (log text is not a contract; a log may serve as a synchronisation signal only where no other
  terminal-state observation exists).
- **Exact call counts** — `Times.Once` / `Times.Never` / `Times.Exactly(N)`; `AtLeastOnce` only when
  that genuinely is the contract.
- **Skip the `Verify` that a `Setup(...).Returns(...)` + returned-value assertion already proves** —
  wrong args would have returned `default` and failed the assert.
- **Verifying what the SUT handed a collaborator**, in order: the exact expected instance (value
  equality); an `It.Is<…>` predicate when one comparison settles it (comparand as an `expected…`
  local above the `Verify`; predicate inline, no `bool` helper); a `Callback` capture + field asserts
  when several independent fields matter or the argument is stale/overwritten by `Verify` time —
  then `Assert.IsNotNull(captured)` carries "the call happened", field asserts follow, and a
  `Times.Once` `Verify` closes with cardinality. Capture only the qualifying argument; never mirror
  whole invocations into a recorded log — ordering is almost always already pinned by the arguments.
- **No reflexive negatives**: `VerifyNoOtherCalls()` and `Times.Never` earn their place only when
  they alone pin a branch — and on background paths, never without a synchronisation point (§11).

## 11. Async

These rules govern genuinely concurrent work:

- `TaskCompletionSource` + `WaitAsync(timeout)` to coordinate; **never** `Thread.Sleep` /
  `Task.Delay` as synchronisation. Class-level timeout field; setup helpers return the wait handle
  with the timeout baked in.
- **Park a collaborator to freeze the SUT mid-operation**: have the mock signal entry, then return a
  never-completing task — overflow, collision and shutdown-mid-work cases become deterministic. To
  make something happen *during* a call, do it in the mock's `Callback` instead.
- **Never depend on beating a configured window** (batch, debounce, poll): park the SUT so the
  inputs are queued before the window opens. Shrinking the interval hides the race, it doesn't
  remove it.
- **A wall-clock bound is never the discriminator** when the branch's own effect is observable:
  assert that effect. A bound standing in for the claim flakes under load and says nothing about
  behaviour. Where nothing but wall time is observable — a private
  budget with no injection seam — make the window's *expiry* the assertion, the one shape load can
  only make more likely to hold, and say so in the class summary.
- **A negative without a synchronisation point is vacuous** — anchor `Times.Never` on a real signal
  the SUT emits under the same conditions, or don't write it.
- No `Interlocked`/`lock` for ordinary test state — awaiting the SUT serializes its work onto the
  test's flow; reach for synchronisation only when the test itself starts concurrent work.

**A suite that gains a real-clock interaction** — a runner loop, a captured console, a background
host — **runs five times in a row**, every result line pasted, before it is handed over. A pasted run
count is not itself a proof against load. The race is fixed, never outrun.
