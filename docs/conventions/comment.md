# Comment conventions

How comments are written in this repo, `//` and `/* */` comments and XML documentation alike;
§ XML documentation holds the rules for XML documentation only.

## Comment the why, never the what

The code already says what it does. A comment earns its place only where a reader who understands
the code would still ask *why is it like this* — a deliberate trade-off, a constraint imposed from
outside, a failure mode that motivated the shape. Everything else is noise that goes stale.

```csharp
// Deadband is applied per stream, not per member: a property and a measuring point on the
// same C# property publish independently — keyed by member name alone, the two streams
// collide and one silently suppresses the other.
```

A comment like that survives a rewrite of the code below it, because it explains a decision rather
than a line.

## A comment is a claim — it carries the same burden as code

A **load-bearing comment can be false**: an ordering justified by an event no handler observes, an
invariant asserted two lines above a call that violates it, advice still given for a reason that
stopped being true. Before writing a comment that states a mechanism,
**verify the mechanism the way you would verify code** — read the implementation it describes, not
the neighbouring prose. A comment describing a guard is not evidence the guard exists. When a change
falsifies a comment elsewhere, fixing that comment is part of the change.

## Build the reasoning up in order

A comment that needs more than one sentence is an explanation, and an explanation has an order:
the setup a reader needs, then the problem it creates, then what the code does about it. Each
sentence readable knowing only the ones before it. State cause before consequence.

## Terse means fewer ideas, not fewer words per idea

Cut whole points that don't need making. Do not cut the connective tissue inside a point that does —
a compressed clause standing in for a sentence is shorter to write and much slower to read. If a
phrase can only be understood by someone who already knows the answer, it is not terse, it is a
reminder — and the reader it was written for does not need it.

## Explain the mechanism, not the domain

The reader is looking at this code and needs to follow *it*. Reach for the surrounding system
(the runtime, the cloud, a consumer) only when the mechanism genuinely cannot be understood
without it.

## Name the concrete failure

"Fails loudly", "handles the error", "would be unsafe" say nothing a reader can check against the
code. Say what actually happens and what it prevents: *a torn read here hands `SetUtcNow` a past
instant, which throws and consumes the already-removed action* — not *this avoids a race*.

## No history, no tickets, no numbers that rot

In a comment and in XML documentation (§ XML documentation) alike: no issue keys, no
RFC/change-doc references, no "now"/"new"/before-after framing, no version numbers — git and the
archived change docs own the history, and a procedure comment describes **today**.

## Write it impersonally

State what the code does, declaratively. No `we`, `our`, or `you`.

## Form

- A `//` or `/* */` comment of more than one line goes in a `/* */` block (or a `//` run) directly
  above the member, indented with it; blank lines separate paragraphs.
- A `//` or `/* */` comment of a single line goes at the site it explains.
- Full sentences with terminal punctuation; a colon-introduced fragment is acceptable for a
  one-line comment on a constant.
- Within a paragraph, a comment — XML documentation included — fills each line to the formatter's
  wrap limit (`WRAP_LIMIT` in `Vion.ServiceProvider.Sdk.sln.DotSettings`) and breaks only where that
  limit forces it; a comment that fits on one line is one line. The limit counts the whole line: its
  indentation and, for a trailing comment, the code before it.

## Read it back before handing it over

Re-read the finished comment for: a comma before `and`/`but` joining independent clauses; parallel
forms after `rather than` / `instead of`; comparisons that name the wrong noun; sentences over
about thirty words (split them) — and, above all, whether every claim in it is one the code below
still makes true.

## XML documentation

### Who gets documented

- **Every interface member.**
- **Every public member of a class that does not implement an interface member.** The caller reads it on hover, without opening the file.
- **A member that implements an interface member carries `<inheritdoc />`**, an explicit implementation included. Never restate the interface's text on it. Where this implementation does something of its own that no other would share and a caller of this class must know, `<remarks>` beneath the `<inheritdoc />` says so — a health monitor's implementation of a going-offline publish explains what its last message marks and why it leaves local state alone, which is true of that class and of no contract.
- **Private and internal members get no XML documentation** — whoever reads one is already in the file, and the hover buys nothing. A *why* on such a member is a line comment above it.
- **Every public type gets documentation too, and so does every public constructor, enum value and positional record parameter.** The package is published, so a caller reads every public symbol on hover. A type's summary says what its name cannot: *Reports the outcome of a registration operation to the cloud* on a type named `RegistrationReporter` adds nothing, while the reason the type exists, a deliberate shape, or a mode that spans every member does. Where the name already says everything — a public constructor, most enum values, a class of constants — the summary is one plain sentence saying what it is, in the words a caller would search for, and nothing more. A positional record's parameters are documented with `<param>` tags inside the record's own block.

### One plain sentence

A `<summary>` is a single declarative sentence saying what the member is. No colon, no clause list, no second sentence. Everything conditional — caveats, edge cases, threading, lifetime — moves to `<remarks>`.

A property's summary is a noun phrase saying what the value is — *The host name or address of the broker to connect to* — never *Gets the …* or *Gets or sets the …*: the accessors are in the signature.

### Complete tags, never a subset

A `<param>` for every parameter of a documented member, type parameters included, a `<returns>` wherever the member returns a value, a `Task` included — on a `Task` it says what completion means. Three of four parameters documented is bitrot waiting to happen: the missing one is the one forgotten when the signature changes.

Tags come in the order the hover shows them: `<summary>`, `<param>`, `<typeparam>`, `<returns>`, `<exception>`, `<remarks>`.

A type or member named in documentation is a `<see cref="…" />`, never bare text: it links on hover, and a rename that misses it is visible. The cref names the member where the sentence means the member, not the type it hangs on, and is qualified where the short name would bind to something else.

### Contract on the interface, behavior on the class

The interface documents what any implementation must provide, in vocabulary the caller already has, and it must stay true for every implementation there could be. A restart method's contract may say that a restart is retried, if retrying is the promise; how many times, and with what backoff, is one implementation's business and belongs on the class. The same goes for file formats, storage layout, the names of files it touches.

### Fix a wrong summary, don't delete the set

A summary that no longer matches the code is corrected in place. Removing the documentation because part of it drifted loses the part that was right.
