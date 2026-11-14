# AGENTS.md

## Project Overview

Azure Cost CLI is a .NET command-line tool that retrieves Azure cost information via the Azure Cost Management API. It is distributed as a .NET global tool (`azure-cost-cli`) and supports multiple output formats.

## Architecture

```
src/
├── Program.cs                  # Entry point, DI setup, Spectre.Console.Cli command registration
├── Commands/                   # CLI commands (one subfolder per command)
│   ├── CommandHelpers.cs       # Shared validation: subscription resolution, timeframe, version
│   ├── AccumulatedCost/        # Default command: accumulated cost overview
│   ├── DailyCost/              # Daily costs grouped by dimension
│   ├── CostByResource/         # Cost breakdown by resource
│   ├── CostByTag/              # Cost breakdown by tag key(s)
│   ├── Budgets/                # Azure budget information
│   ├── DetectAnomaly/          # Anomaly detection on cost data
│   ├── Diff/                   # Compare costs between two timeframes
│   ├── Regions/                # List Azure regions
│   ├── WhatIf/                 # What-if cost scenarios (region/dev-test)
│   ├── Threshold/              # CI/CD cost threshold checks (daily-change, forecast-deviation, service-spike, weekly-average)
│   └── Prices/                 # Price catalog lookup
├── CostApi/                    # Azure API integration layer
│   ├── AzureCostApiRetriever.cs    # Calls Azure Cost Management REST API
│   ├── AzurePriceRetriever.cs      # Calls Azure Retail Prices API
│   ├── AzureRegionsRetriever.cs    # Calls Azure Regions API
│   └── PollyPolicyExtensions.cs    # Retry/resilience policies (Polly)
├── OutputFormatters/           # Output rendering (Console, JSON, Text, Markdown, CSV)
│   └── OutputFormatterFactory.cs   # Factory for formatter dictionary (used by all commands)
└── Infrastructure/             # DI registrar/resolver, extensions, type converters
```

Each command inherits from `AsyncCommand<TSettings>` (Spectre.Console.Cli) and follows a consistent pattern:
- A `*Settings.cs` class defines CLI options/arguments
- A `*Command.cs` class implements the command logic
- `Validate()` uses `CommandHelpers` for subscription resolution and timeframe validation
- Formatters are initialized via `OutputFormatterFactory.Create()`
- Commands call `ICostRetriever` / `IPriceRetriever` / `IRegionsRetriever` (injected via DI)
- Results are rendered through the appropriate `OutputFormatter`

## Prerequisites

- .NET 10.0 SDK (or newer)
- Azure CLI authenticated (`az login`) for cost data access
- An Azure subscription with Cost Management API access

## Build

```bash
# Build the solution (from repo root)
dotnet build src/azure-cost-cli.sln
```

The project targets `net10.0` (LTS) only. The tool sets `RollForward=Major`, so it also runs on machines that only have a newer runtime (e.g. .NET 11). Warnings are treated as errors for NuGet dependency resolution (`NU1605`).

## Test

```bash
# Run all tests
dotnet test src/azure-cost-cli.sln
```

Tests use **xUnit**, **Moq** for mocking, and **Shouldly** for assertions. Tests cover:
- Command validation logic (date ranges, settings, subscription resolution)
- Shared infrastructure (CommandHelpers, OutputFormatterFactory)
- Cost data model parsing (API response deserialization)
- Output formatters (JSON, Text, Markdown, CSV rendering)
- Polly resilience policy behavior

## Run

```bash
# Run from source
dotnet run --project src/azure-cost-cli.csproj -- [command] [options]

# Show all available commands and options
dotnet run --project src/azure-cost-cli.csproj -- --help

# Examples
dotnet run --project src/azure-cost-cli.csproj -- accumulatedCost -o json
dotnet run --project src/azure-cost-cli.csproj -- accumulatedCost -o text
dotnet run --project src/azure-cost-cli.csproj -- dailyCosts --dimension MeterCategory
dotnet run --project src/azure-cost-cli.csproj -- costByResource -o csv
dotnet run --project src/azure-cost-cli.csproj -- budgets -o json
dotnet run --project src/azure-cost-cli.csproj -- detectAnomalies -o text
dotnet run --project src/azure-cost-cli.csproj -- threshold daily-change --percentage 20 --fail-on-threshold
dotnet run --project src/azure-cost-cli.csproj -- threshold service-spike --fixed-amount 200 -o json
dotnet run --project src/azure-cost-cli.csproj -- threshold weekly-average --fixed-amount 50 --fail-on-threshold
```

### Install as global tool (from source)

```bash
dotnet pack src/azure-cost-cli.csproj -c Release
dotnet tool install --global --add-source src/nupkg azure-cost-cli

# Then run with:
azure-cost accumulatedCost -o json
```

## Available Commands

| Command | Description |
|---------|-------------|
| `accumulatedCost` | Show accumulated cost details (default command) |
| `dailyCosts` | Show daily cost grouped by a dimension |
| `costByResource` | Show cost details per resource |
| `costByTag` | Show cost details by tag key(s) |
| `detectAnomalies` | Detect cost anomalies and trends |
| `diff` | Compare costs between two timeframes |
| `budgets` | Show configured budgets |
| `regions` | List available Azure regions |
| `what-if region` | Run what-if cost scenarios across regions |
| `what-if devtest` | Run what-if cost scenarios for dev/test pricing |
| `threshold daily-change` | Trigger if today's cost change vs yesterday exceeds the threshold |
| `threshold forecast-deviation` | Trigger if actual spend deviates from forecast by more than the threshold |
| `threshold service-spike` | Trigger if any service cost spikes beyond the threshold vs previous period |
| `threshold weekly-average` | Trigger if the 7-day average daily cost exceeds the threshold (fixed-amount only) |

## Output Formats

Specify with `-o` / `--output`: `Console` (default), `Json`, `JsonC`, `Text`, `Markdown`, `Csv`

All formatters support all commands. Text and Markdown formatters include region listing, cost-by-tag breakdown, and prices-per-region output.

## Key Dependencies

| Package | Purpose |
|---------|---------|
| `Spectre.Console` / `Spectre.Console.Cli` | CLI framework, rich console output |
| `Azure.Identity` | Azure authentication (DefaultAzureCredential) |
| `Microsoft.Extensions.Http.Polly` | HTTP client resilience (retry, backoff) |
| `CsvHelper` | CSV output formatting |
| `JmesPath.Net` | JMESPath query filtering for JSON output |

## CI/CD

GitHub Actions workflows in `.github/workflows/`:
- **`dotnet.yml`** — Build + test on push/PR to `main`, coverage reporting (min 75%)
- **`dotnet-build-on-tag.yml`** — Package and publish on tag creation
- **`codeql.yml`** — Security scanning

## Coding Conventions

- All commands use `public override` for `ExecuteAsync` and `Validate` methods (Spectre.Console.Cli 0.57.x; 0.55.0 had made them `protected`)
- `ExecuteAsync` signature includes `CancellationToken cancellationToken` parameter
- Subscription resolution and timeframe validation are centralized in `CommandHelpers`
- Formatter dictionaries are created via `OutputFormatterFactory.Create()`
- API calls go through the `CostApi/` layer interfaces, never directly from commands
- Output rendering is delegated to `OutputFormatters/` classes
- HTTP resilience uses Polly retry-after policies for Azure API rate limiting
