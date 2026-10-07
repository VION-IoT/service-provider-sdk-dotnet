# Code style conventions

This document defines the formatting and micro-level idioms every C# file in this repo follows.

## Formatting

Formatting is whatever `cleanupcode` applies through `scripts/cleanup-code.ps1` (`/cleanup`), with the
`Custom: Full Cleanup (excl. optimize usings)` profile in `Vion.ServiceProvider.Sdk.sln.DotSettings`. Never run
cleanup with the `Built-in: Reformat Code` profile: it formats differently from the DotSettings profile.
CI runs the same script with `-Verify`, which fails on any drift.

**Formatter escape hatch:** for the rare span where `cleanupcode` formats inconsistently across OSes
(local vs the Linux CI runner) or where you intentionally hand-format (e.g. an aligned table), wrap
it in `// @formatter:off` / `// @formatter:on` with a short reason comment — `cleanupcode` honors
these on every OS. Use it sparingly and locally, never to opt a whole file out.

## Idioms

- Prefer a collection expression over `.ToArray()` / `.ToList()` wherever the target type is known: `int[] ids = [.. query];`, not `query.ToArray()`. `var x = query.ToArray()` stays — a collection expression has no natural type.
- Prefer a method group over a lambda that only forwards its arguments: `.Callback(cancellationTokenSource.Cancel)`, not `.Callback(() => cancellationTokenSource.Cancel())`.
- No null-forgiving `!` where the compiler already knows the value is non-null — a literal, a `new` expression, a value flow analysis has just checked. ReSharper flags each as a redundant suppression.
- A local initialised to a compile-time constant and never reassigned is `const`, not `var`: `const string forgedServiceName = "vpn\nname";`. Applies to literals and constant expressions, including interpolations of other consts; not to `new` expressions, `Guid.NewGuid()`, or a captured local a lambda mutates.
- Every `using` is written in the file that needs it: no project enables `ImplicitUsings`. The one global using is the test project's `Microsoft.VisualStudio.TestTools.UnitTesting`, which its csproj imports.
