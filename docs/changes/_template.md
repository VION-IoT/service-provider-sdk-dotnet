---
slug: <kebab-case-slug>
status: proposed               # proposed | in-flight | archived
areas: [<area>, <area>]
author: <Name>
created: YYYY-MM-DD
updated: YYYY-MM-DD
---

# <Title>

<!--
Change doc for feature-sized work inside one repo. Lives at docs/changes/YYYY-MM-DD-<slug>.md while
proposed or in-flight, and is moved to docs/changes/archive/ in the pull request that lands it. Ratified before code when a reviewer's
question is open. A repo that keeps a spec corpus adds its own spec-delta section below Full design.
Delete this comment.
-->

## At a glance

### Summary

<≤5 lines: what changes, for whom, why now.>

### Decisions

- **D1 —** <the decision, and its reason in one clause>
- **D2 —** <…>

### Reviewer's questions

<3–5 items, each in one of three states.>

1. `[open]` <question>
2. `[resolved]` <question> — **A:** <answer>
3. `[deferred]` <question> — **Owner:** <name> · **Trigger:** <the event that answers it>

---

## Full design

<The design, with claims about current behaviour citing the file and line that show it.>

## Drift checkpoints

<One line per divergence between Full design and what was built, appended when it happens.>

- YYYY-MM-DD: <what changed and why>

## Tasks

<One commit each. A task is done when its commit is.>

- **T-001** — <task>
- **T-002** — <task>
