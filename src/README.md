# Azure Cost CLI


This is a simple command line tool to get the cost of your Azure subscription. It uses the Azure Cost Management API to get the cost and output the results to the console, text, csv, markdown or JSON. E.g. so it can be used in a workflow to get the cost of your subscription and use it in subsequent steps.

![](https://raw.githubusercontent.com/mivano/azure-cost-cli/main/screenshot.png)

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