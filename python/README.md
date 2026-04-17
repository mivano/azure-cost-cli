# azure-cost-cli (Python)

Python port of the [.NET azure-cost-cli](https://github.com/mivano/azure-cost-cli) tool.

## Features

Full feature parity with the .NET version:

| Command | Description |
|---------|-------------|
| `accumulatedCost` | Accumulated cost overview (default) |
| `dailyCosts` | Daily costs grouped by a dimension |
| `costByResource` | Cost breakdown by resource |
| `costByTag` | Cost breakdown by tag key(s) |
| `detectAnomalies` | Detect cost anomalies and trends |
| `diff` | Compare costs between two timeframes |
| `budgets` | Show configured budgets |
| `regions` | List available Azure regions |
| `what-if region` | What-if cost across regions for VMs |
| `what-if devtest` | What-if Dev/Test pricing for VMs |

**Output formats:** `Console` (rich tables), `Json`, `JsonC` (coloured), `Text`, `Markdown`, `Csv`

## Prerequisites

- Python 3.10+
- Azure CLI authenticated (`az login`) **or** any Azure credential supported by `azure-identity`

## Installation

```bash
# Install directly from the python/ directory
pip install ./python

# Or install in editable mode for development
pip install -e ./python
```

## Usage

```bash
# Show help
azure-cost --help

# Accumulated cost for current billing month (Console output)
azure-cost accumulatedCost

# JSON output
azure-cost accumulatedCost -o Json

# Specify subscription
azure-cost accumulatedCost -s <subscription-id>

# Daily costs grouped by service name
azure-cost dailyCosts --dimension ServiceName

# Cost by resource, top 20, sorted by cost descending
azure-cost costByResource --top 20

# Cost by tag
azure-cost costByTag --tag Environment --tag Team

# Detect anomalies
azure-cost detectAnomalies -o Text

# Compare two time periods (live)
azure-cost diff --source-from 2024-01-01 --source-to 2024-01-31 \
               --from 2024-02-01 --to 2024-02-29

# Compare two JSON snapshots
azure-cost accumulatedCost -o Json > jan.json
azure-cost accumulatedCost -o Json > feb.json
azure-cost diff --compare-from jan.json --compare-to feb.json

# Budget overview
azure-cost budgets -o Markdown

# Filter by resource group
azure-cost accumulatedCost --filter ResourceGroupName=my-rg

# Use USD for all cost values
azure-cost accumulatedCost --use-usd

# CI/CD cost gate (fail if over £500)
azure-cost accumulatedCost --fail-if-over 500
```

## Options (shared across all commands)

| Option | Default | Description |
|--------|---------|-------------|
| `-s / --subscription` | auto | Azure subscription ID |
| `-g / --resource-group` | | Resource group scope |
| `-b / --billing-account` | | Billing account ID |
| `-e / --enrollment-account` | | Enrollment account ID |
| `-o / --output` | `Console` | Output format |
| `-t / --timeframe` | `BillingMonthToDate` | Time period |
| `--from` | | Custom start date (YYYY-MM-DD) |
| `--to` | | Custom end date (YYYY-MM-DD) |
| `--filter` | | Dimension/tag filter e.g. `ResourceGroupName=rg1` |
| `-m / --metric` | `ActualCost` | Cost metric (`ActualCost` or `AmortizedCost`) |
| `--use-usd` | `false` | Force USD currency |
| `--skip-header` | `false` | Skip header (useful when appending output) |
| `--others-cutoff` | `10` | Items before collapsing into Others |
| `--query` | | JMESPath query (Json output only) |
| `--fail-if-over` | | Exit code 1 if total cost exceeds this amount |
| `--debug` | `false` | Print debug information |

## Development

```bash
# Install with dev dependencies
pip install -e "./python[dev]"

# Run tests
cd python
pytest

# Run tests with coverage
pytest --cov=azure_cost_cli --cov-report=term-missing
```

## Architecture

```
python/
├── pyproject.toml                   # Package metadata and dependencies
├── README.md                        # This file
├── azure_cost_cli/
│   ├── __init__.py
│   ├── __main__.py                  # python -m azure_cost_cli entry point
│   ├── cli.py                       # Click CLI, all commands
│   ├── models.py                    # Dataclass models (mirrors .NET records)
│   ├── cost_api.py                  # Azure Cost Management API client
│   ├── price_api.py                 # Azure Retail Prices API client
│   ├── regions_api.py               # Azure Regions API client
│   ├── commands/
│   │   ├── __init__.py
│   │   └── cost_analyzer.py         # Anomaly detection logic
│   └── formatters/
│       ├── __init__.py              # Factory function
│       ├── base.py                  # Abstract base
│       ├── json_formatter.py        # JSON / JsonC output
│       ├── text_formatter.py        # Plain text output
│       ├── markdown_formatter.py    # Markdown output
│       ├── csv_formatter.py         # CSV output
│       └── console_formatter.py    # Rich console output
└── tests/
    ├── test_models.py               # Data model tests
    ├── test_cost_api.py             # API utility tests
    ├── test_cost_analyzer.py        # Anomaly detection tests
    ├── test_formatters.py           # Formatter output tests
    └── test_cli_helpers.py          # CLI helper function tests
```
