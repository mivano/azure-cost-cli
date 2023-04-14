# Azure Cost CLI

This is a simple command line tool to get the cost of your Azure subscription. It uses the Azure Cost Management API to get the cost and output the results to the console or JSON. E.g. so it can be used in a workflow to get the cost of your subscription and use it in subsequent steps.

![](screenshot.png)

## Installation

You can install this tool globally, using the dotnet tool command:

```bash
dotnet tool install --global azure-cost-cli 
```

## Upgrading

When there is a new version available on NuGet, you can use the `dotnet tool update` command to upgrade:

```bash
dotnet tool update --global azure-cost-cli 
```

With a `--version` parameter, you can specify a specific version to install. Use the `--no-cache` parameter to force a re-download of the package if it cannot find the latest version.

## Usage

You can invoke the tool using the `azure-cost` command. You can use the `--help` parameter to get a list of all available options.

```bash
azure-cost --help
```

Which will show:

```
USAGE:
    Azure Cost [OPTIONS]

EXAMPLES:
    Azure Cost show -s 00000000-0000-0000-0000-000000000000
    Azure Cost show -s 00000000-0000-0000-0000-000000000000 -o json

OPTIONS:
    -h, --help            Prints help information
    -s, --subscription    The subscription id to use
    -o, --output          The output format to use. Defaults to Console (Console, Json)
    -t, --timeframe       The timeframe to use for the costs. Defaults to BillingMonthToDate. When set to Custom, specify the from and to dates using the --from and --to options
        --from            The start date to use for the costs. Defaults to the first day of the previous month
        --to              The end date to use for the costs. Defaults to the current date

COMMANDS:
    show    Show the cost details for a subscription
```

If you do not specify a subscription id, it will fetch the actively selected one of the `az cli` instead. 

## Authentication

To make the call to the Azure cost API, you do need to run this from a user account with permissions to access the cost overview of the subscription. Further more, it needs to find the active credentials and it does so by using the `ChainedTokenCredential` provider which will look for the `az cli` token first. Make sure to run `az login` (with optionally the `--tenant` parameter) to make sure you have an active session.

## To do

- [x] Show time range the report is based on
- [x] Open source it on GitHub!
- [x] Show the forecasted cost
- [ ] Set thresholds, so it can return an error code
- [ ] Generate markdown, so you can include it in a workflow job summary
- [ ] More options to set and filter on
- [x] Validate date ranges
- [x] Workflow to push to NuGet