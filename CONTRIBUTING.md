# Contributing to AURA

## Commit messages

- Commits are authored by the project owner's account. Do **not** add AI co-author trailers
  (for example `Co-authored-by: Claude …`, `… Codex …`, `… Copilot …`) or "Generated with …"
  lines to commit messages or pull request descriptions, whichever tool drafted the change.
- Keep each change in its own commit with a message that says what changed and why.

The rule is enforced twice:

1. **Locally:** `.githooks/commit-msg` rejects such lines. Enable it once per clone:

   ```powershell
   git config core.hooksPath .githooks
   ```

2. **On GitHub:** `.github/workflows/commit-attribution.yml` scans every commit reachable from
   each push and pull request, and fails the check if any commit contains such a line.

A co-author line for a real human contributor is still allowed.

## Before opening or merging a pull request

Run the checks listed in [AURA/docs/MANUAL_FEATURE_TEST_CHECKLIST.md](AURA/docs/MANUAL_FEATURE_TEST_CHECKLIST.md).
Use synthetic receipts from `AURA/test_kit/` only, and never commit secrets, real receipts,
databases, `.tmp/`, `test_kit/results/` or personal study folders.
