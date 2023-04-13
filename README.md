# Azure Cost CLI

This is a simple tool to get the cost of your Azure subscription. It uses the Azure Cost Management API to get the cost of your subscription. It can be used in a workflow to get the cost of your subscription and use it in subsequent steps.

![](screenshot.png)

## How to install

You can install this tool globally, using the dotnet tool command:

```bash
dotnet tool install --global azure-cost-cli 
```

## Upgrading

When there is a new version available, you can use the dotnet tool update command to upgrade:

```bash
dotnet tool update --global azure-cost-cli 
```

With a `--version` parameter, you can specify a specific version to install. Use the `--no-cache` parameter to force a re-download of the package if it cannot find the latest version.

## How to use

You can invoke the tool using the `azure-cost` command. You can use the `--help` parameter to get a list of all available options.

```bash
azure-cost --help
```

## To do

- [x] Show time range the report is based on
- [x] Open source it on GitHub!
- [x] Show the forecasted cost
- [ ] Set thresholds, so it can return an error code
- [ ] Generate markdown, so you can include it in a workflow job summary
- [ ] More options to set and filter
- [x] Validate date ranges
- [x] Workflow to push to NuGet