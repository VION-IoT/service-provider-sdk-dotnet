---
description: Run ReSharper cleanupcode (the same cleanup the CI style gate enforces) and apply fixes before a PR
---

Run the repo's canonical code-cleanup and report the result:

1. Execute `pwsh scripts/cleanup-code.ps1` from the repo root. It restores the pinned
   JetBrains tool, builds the solution, and runs the exact `dotnet jb cleanupcode`
   invocation the CI "verify code style" gate runs (profile
   `Custom: Full Cleanup (excl. optimize usings)`, the same one ReSharper/Rider apply
   on save), applying fixes in place.
2. Summarize what changed from its `git diff --stat` output.
3. If it applied changes, remind the user to commit them before opening the PR (or do
   so if appropriate). If it reported "Already clean", say so.

This is the same cleanup CI enforces, so running it now keeps the style gate from
failing the PR. The script captures cleanupcode's (benign, noisy) output and only
surfaces it if cleanupcode itself fails — so a quiet run means success.
