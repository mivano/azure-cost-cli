---
name: dependabot-updates
description: Manage open Dependabot PRs by applying updates locally, validating with build and tests, and creating a combined PR. Use when asked to review, merge, or handle Dependabot dependency updates.
---

# Dependabot Updates Skill

Apply open Dependabot dependency updates in a controlled, validated way. Never push directly to `main`.

## Workflow

### 1. List and categorize open Dependabot PRs

Use the GitHub MCP tools to list open PRs authored by `dependabot[bot]`:

- Retrieve all open PRs and filter to Dependabot PRs
- Categorize each update by type: **NuGet package** (`.csproj` changes) or **GitHub Actions** (workflow YAML changes)
- Note the version bump level: **major**, **minor**, or **patch**

Present a summary table to the user:

| PR | Package/Action | From → To | Type | Bump |
|----|---------------|-----------|------|------|

> **Major version bumps**: Flag these prominently — they are more likely to introduce breaking changes and may warrant individual review rather than batching.

### 2. Prepare a branch

- Fetch latest from `origin/main`: `git fetch origin main`
- Create a new branch from `origin/main`: `git checkout -b dependabot/combined-updates origin/main`
- If only a single Dependabot PR is open, prefer working with that PR's branch directly instead of creating a combined branch

### 3. Apply updates incrementally

Apply each update one at a time to the relevant files:

- **NuGet packages**: Update `<PackageReference>` version attributes in `.csproj` files (both `src/` and `tests/` projects)
- **GitHub Actions**: Update `uses:` version tags in `.github/workflows/*.yml` files

After applying each update, track its status (applied, not yet validated).

### 4. Build

```bash
dotnet build src/azure-cost-cli.sln
```

The build must succeed with **0 errors**. Pre-existing warnings are acceptable.

### 5. Run tests

```bash
dotnet test src/azure-cost-cli.sln
```

All tests must pass.

### 6. Smoke test the application

Run a quick smoke test to verify the application starts correctly:

```bash
dotnet run --project src/azure-cost-cli.csproj -- --help
```

This should print the help text without errors.

### 7. Handle failures

If the build or tests fail after applying updates:

1. Identify which update(s) caused the failure
2. Revert the failing update(s)
3. Re-run build and tests to confirm the remaining updates work
4. Present a pass/fail summary table to the user:

| PR | Package/Action | Status | Notes |
|----|---------------|--------|-------|

5. Ask the user whether to proceed with only the passing updates or abort entirely

> **Never silently skip broken updates.** Always inform the user about what failed and why.

### 8. Commit and push

Commit all validated changes with a descriptive message listing each update and referencing the original PR numbers. Push the branch to origin.

### 9. Create a PR

**Ask the user for confirmation before creating the PR.**

Create a pull request targeting `main` with:

- A title like: `Bump dependencies: [summary of updates]`
- A body that includes:
  - The full summary table of applied updates
  - Links to each original Dependabot PR (e.g., `Closes #123`)
  - Any updates that were excluded due to failures, with reasons

Using `Closes #NNN` for each Dependabot PR ensures they auto-close when the combined PR is merged.

### 10. Post-merge cleanup

After the PR is merged (by the user or CI), the original Dependabot PRs referenced with `Closes` will be automatically closed by GitHub.

## Important rules

- **Never push directly to `main`** — always go through a pull request
- **Branch from `origin/main`** — ensure the branch is based on the latest remote state
- **GitHub Actions updates are not validated by `dotnet build`** — note in the PR that workflow changes rely on CI for full validation
- **Ask before proceeding** — show the summary and get user confirmation before creating the PR
