# Azure Cost CLI

[![NuGet Badge](https://img.shields.io/nuget/dt/azure-cost-cli)](https://www.nuget.org/packages/azure-cost-cli)
[![Latest release](https://img.shields.io/github/v/release/mivano/azure-cost-cli)](https://github.com/mivano/azure-cost-cli/releases/latest)
[![Latest build](https://img.shields.io/github/actions/workflow/status/mivano/azure-cost-cli/dotnet.yml)](https://github.com/mivano/azure-cost-cli/actions/workflows/dotnet.yml)

This is a simple command line tool to get the cost of your Azure subscription. It uses the Azure Cost Management API to get the cost and output the results to the console, text, csv, markdown or JSON. E.g. so it can be used in a workflow to get the cost of your subscription and use it in subsequent steps.

![](screenshot.png)

Besides showing the accumulated cost, it can also show daily cost, extract resource (costs) and list budgets. 

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="screenshot_daily_metercategory_dark.png">
  <source media="(prefers-color-scheme: light)" srcset="screenshot_daily_metercategory.png">
  <img alt="Daily overview grouped by the meter category dimension." src="screenshot_daily_metercategory.png">
</picture>

It can also detect anomalies and trends in the cost, which can be used to further automate reporting.

![](screenshot_anomalies.png)

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

You can invoke the tool using the `azure-cost` command. You can use the `--help` parameter to get a list of all available options. Some of the commands have additional parameters which can be shown by adding the specific command with the `--help` option.

```bash
azure-cost --help
```

Which will show:

```bash
USAGE:
    azure-cost [OPTIONS]

EXAMPLES:
    azure-cost accumulatedCost -s 00000000-0000-0000-0000-000000000000
    azure-cost accumulatedCost -o json
    azure-cost costByResource -s 00000000-0000-0000-0000-000000000000 -o text
    azure-cost dailyCosts --dimension MeterCategory
    azure-cost diff   --compare-to new.json --compare-from old.json 
    azure-cost budgets -s 00000000-0000-0000-0000-000000000000
    azure-cost detectAnomalies --dimension ResourceId --recent-activity-days 4
    azure-cost costByTag --tag cost-center

OPTIONS:
    -h, --help            Prints help information
        --debug           Increase logging verbosity to show all debug logs  
    -s, --subscription    The subscription id to use. Will try to fetch the active id if not specified 
    -g, --resource-group  The resource group to scope the request to. Need to be used in combination with the subscription id 
    -b, --billing-account The billing account id to use 
    -e, --enrollment-account The enrollment account id to use    
    -o, --output          The output format to use. Defaults to Console (Console, Json, JsonC, Markdown, Text, Csv)
    -t, --timeframe       The timeframe to use for the costs. Defaults to BillingMonthToDate. When set to Custom, specify the from and to dates using the --from and --to options
        --from            The start date to use for the costs. Defaults to the first day of the previous month
        --to              The end date to use for the costs. Defaults to the current date
        --others-cutoff    10         The number of items to show before collapsing the rest into an 'Others' item                                                                
        --query           JMESPath query string. See http://jmespath.org/ for more information and examples  
        --useUSD          Force the use of USD for the currency. Defaults to false to use the currency returned by the API        
        --skipHeader      Skip header creation for specific output formats. Useful when appending the output from multiple runs into one file. Defaults to false 
        --filter          Filter the output by the specified properties. Defaults to no filtering and can be multiple values.
        --includeTags     Include Tags from the selected dimension. Valid only for DailyCost report and output to Json, JsonC or Csv. Ignored in the rest of reports and output formats.
    -m, --metric           ActualCost    The metric to use for the costs. Defaults to ActualCost. (ActualCost, AmortizedCost)    
    --costApiBaseAddress  The base address of the cost API. Defaults to https://management.azure.com/, but can be set to a different value to use a different cloud, like https://management.usgovcloudapi.net/
    --priceApiBaseAddress The base address of the price API. Defaults to https://prices.azure.com/, but can be set to a different value to use a different cloud, like https://prices.azure.us/ for government

COMMANDS:
    accumulatedCost    Show the accumulated cost details
    costByResource     Show the cost details by resource
    costByTag          Show the cost details by the provided tag key(s)
    dailyCosts         Show the daily cost by a given dimension
    diff                            Show the cost difference between two timeframes 
    detectAnomalies    Detect anomalies and trends  
    budgets            Get the available budgets   
    regions            Get the available Azure regions 
    what-if            Run what-if scenarios   

```

Starting from version `0.35`, you can select a different scope besides only subscription. Specify subscription id and resourcegroup name, billing account and/or enrollment account to scope the request to that level. 


> When you do not specify a subscription id, it will fetch the actively selected one of the `az cli` instead. 

> If the application is not working properly, you can use the `--debug` parameter to increase the logging verbosity and see more details.

> This tool uses the Azure Cost Management API to get the cost. Not all subscriptions have access to this API. To check if your subscription has access, you can use the `az account subscription show --subscription-id yourid --query '[subscriptionPolicies.quotaId]' -o tsv
` command. Validate the resulting quota id with the ones on the [Microsoft list](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/understand-cost-mgt-data#supported-microsoft-azure-offers) to see if it is supported. Running the tool with the `--debug` parameter will also show the quota id that is used.

> There is a pretty strict rate limit on the cost api; the calls are retried after a 429 is received, but it might take a while before the call succeeds as it honors the retry time out.

## Authentication

To make the call to the Azure cost API, you do need to run this from a user account with permissions to access the cost overview of the subscription. Further more, it needs to find the active credentials and it does so by using the `ChainedTokenCredential` provider which will look for the `az cli` token first. Make sure to run `az login` (with optionally the `--tenant` parameter) to make sure you have an active session.

## Use in a GitHub workflow

You can use this tool in a GitHub workflow to get the cost of your subscription and store the results in markdown as a Job Summary. This can be used to get a quick overview of the cost of your subscription. Have a look at the [workflow](https://github.com/mivano/azure-cost-cli/actions/workflows/create-markdown.yml) in this repository for an example output.

```yaml
name: Azure Cost CLI Workflow

on:
  workflow_dispatch:
    inputs:
      az-subscription-id:
        description: 'Azure Subscription ID'
        required: true
jobs:
  run-azure-cost-cli:
    runs-on: ubuntu-latest
    steps:
      - name: Azure login
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Install Azure Cost CLI
        run: dotnet tool install -g azure-cost-cli

      - name: Run Azure Cost CLI
        run: azure-cost accumulatedCost -o markdown --subscription ${{ github.event.inputs.az-subscription-id }} >> $GITHUB_STEP_SUMMARY

```

The last step outputs the markdown to the Job Summary. This can be used to show the cost of the subscription in the workflow summary. Use it on a schedule to get for example a daily overview. Alternatively you can use the `-o json` parameter to get the results in JSON format and use it for further processing.

## Available commands

### Accumulated Cost

This will retrieve the accumulated cost of the subscription. This is the total cost of the subscription since the beginning of the period specified. We will try to fetch the forecast as well and organise by location, type and resource group. You can use the different formatters to get the results in different formats.

```bash
azure-cost accumulatedCost -s 574385a9-08e9-49fe-91a2-27660d92b8f5
```

> This is the default command when you do not specify a command.

### Cost By Resource

This will retrieve the cost of the subscription by resource. This will fetch the resource details including the meter information. It is up to the formatter how this is returned. Use the `json` formatter to get the full details.

```bash
azure-cost costByResource -s 574385a9-08e9-49fe-91a2-27660d92b8f5 -o json
```

If you are only interested in the cost of the resources, you can exclude the meter details using the `--exclude-meter-details` parameter.

```bash
azure-cost costByResource -s 574385a9-08e9-49fe-91a2-27660d92b8f5 --exclude-meter-details
```

Do keep in mind that with the `--metric` you can either request the ActualCost or the AmortizedCost cost, but not both at the same time. The default is ActualCost.

A resource can be in multiple resource locations, like Intercontinental and West Europe. When you use `--exclude-meter-details`, the resource will be listed once and the locations will be combined.

You can parse out the resource name, group name and subscription id from the ResourceId field. The format is `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}`.

### Cost By Tag

This will retrieve the cost of the subscription by the provided tag key(s). So if you tag your resources with, e.g. a `cost-center` or a `creator`, you can retrieve the cost of the subscription by those tags. You can specify multiple tags by using the `--tag` parameter multiple times. 

```bash
azure-cost costByTag  --tag cost-center --tag creator
```

The csv, json(c) and console output will render the results, either hierarchically or flattened in the case of the csv export.

If you require more formatters, let me know!

### Daily Costs

The daily overview fetches the cost of the subscription for each day in the specified period. It will show the total cost of the day and the cost per dimension. The dimension is the resource group by default, but you can specify a different one using the `--dimension` parameter. 

For example:

```bash 
azure-cost dailyCosts -s 574385a9-08e9-49fe-91a2-27660d92b8f5 --dimension ResourceLocation
```

![](screenshot_daily_location.png)

The default dimension is the resource group.

```bash 
azure-cost dailyCosts 
```

![](screenshot_daily_resourcegroup.png)

The above screenshots show the default console output, but the other formatters can also be used.

The available dimensions are: `ResourceGroup`,`ResourceGroupName`,`ResourceLocation`,`ConsumedService`,`ResourceType`,`ResourceId`,`MeterId`,`BillingMonth`,`MeterCategory`,`MeterSubcategory`,`Meter`,`AccountName`,`DepartmentName`,`SubscriptionId`,`SubscriptionName`,`ServiceName`,`ServiceTier`,`EnrollmentAccountName`,`BillingAccountId`,`ResourceGuid`,`BillingPeriod`,`InvoiceNumber`,`ChargeType`,`PublisherType`,`ReservationId`,`ReservationName`,`Frequency`,`PartNumber`,`CostAllocationRuleName`,`MarkupRuleName`,`PricingModel`,`BenefitId`,`BenefitName`

### Include Tags
This option allows to include the dimensions' Tags in the same row. Tags allow cost analysis customization. Adding the Tags from the dimension allows complementary analysis in tools like Power BI. This option is enabled for DailyCost report and for Json, JsonC, and Csv expor formats. Using other formats, ignores the option. 

The following query shows the daily costs for subscription x group by resource group name including the tags for the resource group ready to export to Csv:

```bash 
azure-cost dailyCosts -s XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX --dimension ResourceGroupName --includeTags -o Csv
```

That would extend into a column called Tags the resource group tags in Json format:
```bash 
[""\""cost-center\"":\""my_cost_center\"""",""\""owner\"":\""my_email@email.com\""""]
```
Note that the Json column should be parsed in the analytical tool.


### Detect Anomalies

Based on the daily cost data, this command will try to detect anomalies and trends. It will scan for the following anomalies:

- Cost that is stopped; although it is good that the cost is stopped, it might be an indication that something is wrong.
- Cost that is appearing; the cost was not there before and is now adding to the overall cost.
- A spike in cost; a deviation from the normal cost pattern
- A gradual increase in cost; so a service is growing in cost over time.

There are a number of settings you can use to finetune the detection.

```
--dimension               ResourceGroupName    The grouping to use. E.g. ResourceGroupName, Meter, ResourceLocation, etc. Defaults to ResourceGroupName                                               
--recent-activity-days    7                    The number of days to use for recent activity. Defaults to 7                                                                                           
--significant-change      0,75                 The significant change in cost to use. Defaults to 0.75 (75%)                                                                                          
--steady-growth-days      7                    The number of days to use for steady growth. Defaults to 7                                                                                             
--threshold-cost          2                    The thresshold cost to use. Values lower than this are excluded. Defaults to 2.00      
--exclude-removed-costs   false                Exclude removed costs. Defaults to false. 
```

All the different formatters can be used to output the data, so you can further process it with the JSON or CSV output, send it via email with the text formatter, or use the markdown formatter to include it in a GitHub workflow. The console output will render the different anomalies in different categories and point out the moment in time when the anomaly was detected.

![](screenshot_anomalies.png)

```bash 
azure-cost detectAnomalies
```

### Budgets

This will retrieve the available budgets for the subscription. It will show the current status of the budget and the amount of the budget. As well as listing the configured notifications. 

```bash
azure-cost budgets -s 574385a9-08e9-49fe-91a2-27660d92b8f5 
```

### Regions

Retrieve a list of available regions. Besides the location and supported compliances, it will also show the available sustainability information.

```bash
azure-cost regions
```

> Not all the formatters are supported for this command. Let me know if there is a need.


### Diff

Generate a difference between two cost outputs. Create the json export first using 

```bash
azure-cost accumulatedCost -o json > filename.json
```

Save this result, store it in source control etc and create a new snapshot at a later time.

When you have both a source file and a target file, you can use the diff command to show the difference between the two. 

```bash
azure-cost diff --compare-to new.json --compare-from old.json 
```

This will give an output like:

```bash
                                                                                                           Azure Cost Diff                                                                                                            
                                                                                                  Source: (01/03/2025 to 20/03/2025)                                                                                                  
                                                                                                  Target: (01/03/2025 to 20/03/2025)                                                                                                  
                                                                                                                                                                                                                                    
╭─Services────────────────────────────────────────────────────╮                                                                                                                                                                     
│                                                             │                                                                                                                                                                     
│   Name                 Source      Target      Change       │                                                                                                                                                                     
│ ─────────────────────────────────────────────────────────── │                                                                                                                                                                     
│   Microsoft Dev Box    12,27 EUR   22,27 EUR   +10,00 EUR   │                                                                                                                                                                     
│   Azure Monitor        5,21 EUR    15,21 EUR   +10,00 EUR   │                                                                                                                                                                     
│   Azure App Service    7,97 EUR    7,97 EUR    +0,00 EUR    │                                                                                                                                                                     
│   Container Registry   3,07 EUR    3,07 EUR    +0,00 EUR    │                                                                                                                                                                     
│   Azure DNS            0,30 EUR    2,30 EUR    +2,00 EUR    │                                                                                                                                                                     
│   Log Analytics        0,29 EUR    0,29 EUR    +0,00 EUR    │                                                                                                                                                                     
│   Key Vault            0,00 EUR    0,00 EUR    +0,00 EUR    │                                                                                                                                                                     
│   Storage              0,00 EUR    0,00 EUR    +0,00 EUR    │                                                                                                                                                                     
│   Bandwidth            0,00 EUR    0,00 EUR    +0,00 EUR    │                                                                                                                                                                     
│   SUBTOTAL             29,11 EUR   51,11 EUR   +22,00 EUR   │                                                                                                                                                                     
│                                                             │                                                                                                                                                                     
╰─────────────────────────────────────────────────────────────╯                                                                                                                                                                     
╭─Locations─────────────────────────────────────────╮                                                                                                                                                                               
│                                                   │                                                                                                                                                                               
│   Name       Source      Target      Change       │                                                                                                                                                                               
│ ───────────────────────────────────────────────── │                                                                                                                                                                               
│   EU West    28,81 EUR   48,81 EUR   +20,00 EUR   │                                                                                                                                                                               
│   Unknown    0,30 EUR    0,30 EUR    +0,00 EUR    │                                                                                                                                                                               
│   SUBTOTAL   29,11 EUR   49,11 EUR   +20,00 EUR   │                                                                                                                                                                               
│                                                   │                                                                                                                                                                               
╰───────────────────────────────────────────────────╯                                                                                                                                                                               
╭─Resource Groups───────────────────────────────────────────────────╮                                                                                                                                                               
│                                                                   │                                                                                                                                                               
│   Name                       Source      Target      Change       │                                                                                                                                                               
│ ───────────────────────────────────────────────────────────────── │                                                                                                                                                               
│   rg-scitor-prd-weu          16,25 EUR   26,25 EUR   +10,00 EUR   │                                                                                                                                                               
│   devbox                     12,27 EUR   2,27 EUR    -10,00 EUR   │                                                                                                                                                               
│   globaldevopsexperience     0,30 EUR    1,30 EUR    +1,00 EUR    │                                                                                                                                                               
│   defaultresourcegroup-weu   0,29 EUR    0,29 EUR    +0,00 EUR    │                                                                                                                                                               
│   SUBTOTAL                   29,11 EUR   30,11 EUR   +1,00 EUR    │                                                                                                                                                               
│                                                                   │                                                                                                                                                               
╰───────────────────────────────────────────────────────────────────╯                                                                                                                                                               
╭─Summary─────────────────────────────────────────────╮                                                                                                                                                                             
│ ╭─────────────┬───────────┬───────────┬───────────╮ │                                                                                                                                                                             
│ │ Comparison  │ Source    │ Target    │ Change    │ │                                                                                                                                                                             
│ ├─────────────┼───────────┼───────────┼───────────┤ │                                                                                                                                                                             
│ │ TOTAL COSTS │ 29,11 EUR │ 32,01 EUR │ +2,90 EUR │ │                                                                                                                                                                             
│ ╰─────────────┴───────────┴───────────┴───────────╯ │                                                                                                                                                                             
╰─────────────────────────────────────────────────────╯                                                                                                                                                                             

```

Also, the JSON output can be used to further process the data. It will output only the differences between the two files where the cost is the actual difference.

> Not all the formatters are supported for this command. Let me know if there is a need for another formatter.


### What-if scenarios

This command allows you to run what-if scenarios. It will show the cost of the subscription if you would make changes to either usage or rates. 

#### Regions

The what-if regions command compares your virtual machines in the given subscription with prices of the same virtual machine in different regions. It will show the current cost and the cost if you would move the virtual machine to a different region. 

```bash
azure-cost what-if regions
```

A typical output to the console might be like this:

```bash
Prices per region for a914b3f4-fe8b-4e94-a0d3-2938540d59c6 between 01/09/2023 and 05/10/2023
└── Resource: idlemachinetest
    ├── Group: TEST-IDLE-MACHINE
    ├── Product: Virtual Machines BS Series - B2ms - EU West
    ├── Total quantity: 244 (100 Hours)
    ├── Current cost: 21,93 EUR
    └── ╭──────────────────┬─────────────────────────┬─────────────────────────┬───────────┬─────────────────────────┬──────────────────┬────────────────────────┬───────────────────╮
        │ Region           │ Retail Price            │ Cost                    │ Deviation │ 1 Year Savings Plan     │ 1 Year Deviation │ 3 Years Savings Plan   │ 3 Years Deviation │
        ├──────────────────┼─────────────────────────┼─────────────────────────┼───────────┼─────────────────────────┼──────────────────┼────────────────────────┼───────────────────┤
        │ US West 2        │            0,079151 EUR │               19,31 EUR │ -13,33%   │            0,053308 EUR │ -41,63%          │           0,035666 EUR │ -60,95%           │
        │ US East 2        │            0,079151 EUR │               19,31 EUR │ -13,33%   │            0,062664 EUR │ -31,39%          │           0,042291 EUR │ -53,69%           │
        │ US West 3        │            0,079151 EUR │               19,31 EUR │ -13,33%   │            0,062664 EUR │ -31,39%          │           0,042291 EUR │ -53,69%           │
        │ US East          │            0,079151 EUR │               19,31 EUR │ -13,33%   │            0,053308 EUR │ -41,63%          │           0,035666 EUR │ -60,95%           │
        │ US North Central │            0,079151 EUR │               19,31 EUR │ -13,33%   │            0,053308 EUR │ -41,63%          │           0,035666 EUR │ -60,95%           │
        │ SE Central       │            0,082196 EUR │               20,06 EUR │ -10,00%   │            0,062773 EUR │ -31,27%          │           0,043186 EUR │ -52,71%           │
        │ IN West Jio      │            0,085240 EUR │               20,80 EUR │ -6,67%    │            0,057563 EUR │ -36,97%          │           0,037557 EUR │ -58,88%           │
        │ IN Central Jio   │            0,085240 EUR │               20,80 EUR │ -6,67%    │            0,057563 EUR │ -36,97%          │           0,037557 EUR │ -58,88%           │
        │ IN Central       │            0,085240 EUR │               20,80 EUR │ -6,67%    │            0,057580 EUR │ -36,95%          │           0,037548 EUR │ -58,89%           │
        │ EU North         │            0,086572 EUR │               21,12 EUR │ -5,21%    │            0,066106 EUR │ -27,62%          │           0,045476 EUR │ -50,21%           │
        │ CA East          │            0,088284 EUR │               21,54 EUR │ -3,33%    │            0,059645 EUR │ -34,69%          │           0,039922 EUR │ -56,29%           │
        │ CA Central       │            0,088284 EUR │               21,54 EUR │ -3,33%    │            0,059645 EUR │ -34,69%          │           0,039922 EUR │ -56,29%           │
        │ UK West          │            0,089426 EUR │               21,82 EUR │ -2,08%    │            0,070798 EUR │ -22,48%          │           0,047780 EUR │ -47,68%           │
        │ UK South         │            0,089806 EUR │               21,91 EUR │ -1,67%    │            0,068028 EUR │ -25,51%          │           0,047651 EUR │ -47,82%           │
        │ FR Central       │            0,089806 EUR │               21,91 EUR │ -1,67%    │            0,068028 EUR │ -25,51%          │           0,047651 EUR │ -47,82%           │
        │ IT North         │            0,091329 EUR │               22,28 EUR │ 0,00%     │                         │                  │                        │                   │
        │ DE West Central  │            0,091329 EUR │               22,28 EUR │ 0,00%     │            0,070734 EUR │ -22,55%          │           0,048724 EUR │ -46,65%           │
        │ EU West          │            0,091329 EUR │               22,28 EUR │ 0,00%     │            0,070725 EUR │ -22,56%          │           0,048733 EUR │ -46,64%           │
        │ US Gov Virginia  │            0,092851 EUR │               22,66 EUR │ 1,67%     │                         │                  │                        │                   │
        │ US Gov AZ        │            0,092851 EUR │               22,66 EUR │ 1,67%     │                         │                  │                        │                   │
        │ US West          │            0,094373 EUR │               23,03 EUR │ 3,33%     │            0,074498 EUR │ -18,43%          │           0,049140 EUR │ -46,19%           │
        │ US Central       │            0,094944 EUR │               23,17 EUR │ 3,96%     │            0,075167 EUR │ -17,70%          │           0,050728 EUR │ -44,46%           │
        │ US South Central │            0,094944 EUR │               23,17 EUR │ 3,96%     │            0,075167 EUR │ -17,70%          │           0,050728 EUR │ -44,46%           │
        │ US West Central  │            0,094944 EUR │               23,17 EUR │ 3,96%     │            0,075167 EUR │ -17,70%          │           0,050728 EUR │ -44,46%           │
        │ AE North         │            0,094944 EUR │               23,17 EUR │ 3,96%     │            0,072546 EUR │ -20,57%          │           0,049874 EUR │ -45,39%           │
        │ QA Central       │            0,095134 EUR │               23,21 EUR │ 4,17%     │            0,072682 EUR │ -20,42%          │           0,049974 EUR │ -45,28%           │
        │ IL Central       │            0,095134 EUR │               23,21 EUR │ 4,17%     │            0,064691 EUR │ -29,17%          │           0,043762 EUR │ -52,08%           │
        │ KR South         │            0,098939 EUR │               24,14 EUR │ 8,33%     │            0,078330 EUR │ -14,23%          │           0,052863 EUR │ -42,12%           │
        │ KR Central       │            0,098939 EUR │               24,14 EUR │ 8,33%     │            0,064202 EUR │ -29,70%          │           0,045413 EUR │ -50,28%           │
        │ CH North         │            0,100461 EUR │               24,51 EUR │ 10,00%    │            0,079535 EUR │ -12,91%          │           0,053677 EUR │ -41,23%           │
        │ AU Central       │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,073484 EUR │ -19,54%          │           0,050945 EUR │ -44,22%           │
        │ AU Central 2     │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,073484 EUR │ -19,54%          │           0,050945 EUR │ -44,22%           │
        │ AU Southeast     │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,079837 EUR │ -12,58%          │           0,053880 EUR │ -41,00%           │
        │ NO East          │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,079837 EUR │ -12,58%          │           0,053880 EUR │ -41,00%           │
        │ PL Central       │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,078092 EUR │ -14,49%          │           0,053809 EUR │ -41,08%           │
        │ AP Southeast     │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,068048 EUR │ -25,49%          │           0,044623 EUR │ -51,14%           │
        │ AU East          │            0,100842 EUR │               24,61 EUR │ 10,42%    │            0,073484 EUR │ -19,54%          │           0,050945 EUR │ -44,22%           │
        │ ZA North         │            0,102745 EUR │               25,07 EUR │ 12,50%    │            0,078435 EUR │ -14,12%          │           0,053941 EUR │ -40,94%           │
        │ JA East          │            0,103696 EUR │               25,30 EUR │ 13,54%    │            0,076071 EUR │ -16,71%          │           0,053435 EUR │ -41,49%           │
        │ SE South         │            0,106550 EUR │               26,00 EUR │ 16,67%    │            0,084356 EUR │ -7,64%           │           0,056930 EUR │ -37,66%           │
        │ AP East          │            0,111307 EUR │               27,16 EUR │ 21,87%    │            0,075076 EUR │ -17,80%          │           0,049264 EUR │ -46,06%           │
        │ US Gov TX        │            0,111782 EUR │               27,27 EUR │ 22,39%    │                         │                  │                        │                   │
        │ IN South         │            0,112258 EUR │               27,39 EUR │ 22,92%    │            0,088875 EUR │ -2,69%           │           0,059979 EUR │ -34,33%           │
        │ IN West          │            0,113209 EUR │               27,62 EUR │ 23,96%    │            0,089628 EUR │ -1,86%           │           0,060488 EUR │ -33,77%           │
        │ JA West          │            0,114161 EUR │               27,86 EUR │ 25,00%    │            0,090381 EUR │ -1,04%           │           0,060996 EUR │ -33,21%           │
        │ DE North         │            0,118917 EUR │               29,02 EUR │ 30,21%    │            0,094147 EUR │ 3,09%            │           0,063538 EUR │ -30,43%           │
        │ AE Central       │            0,118917 EUR │               29,02 EUR │ 30,21%    │            0,094147 EUR │ 3,09%            │           0,063538 EUR │ -30,43%           │
        │ BR South         │            0,127479 EUR │               31,10 EUR │ 39,58%    │            0,078540 EUR │ -14,00%          │           0,057786 EUR │ -36,73%           │
        │ FR South         │            0,128431 EUR │               31,34 EUR │ 40,62%    │            0,101679 EUR │ 11,33%           │           0,068621 EUR │ -24,86%           │
        │ ZA West          │            0,129025 EUR │               31,48 EUR │ 41,27%    │            0,102149 EUR │ 11,85%           │           0,068938 EUR │ -24,52%           │
        │ NO West          │            0,130333 EUR │               31,80 EUR │ 42,71%    │            0,103185 EUR │ 12,98%           │           0,069637 EUR │ -23,75%           │
        │ CH West          │            0,131037 EUR │               31,97 EUR │ 43,48%    │            0,103742 EUR │ 13,59%           │           0,070013 EUR │ -23,34%           │
        │ BR Southeast     │            0,166484 EUR │               40,62 EUR │ 82,29%    │            0,131806 EUR │ 44,32%           │           0,088953 EUR │ -2,60%            │
        ╰──────────────────┴─────────────────────────┴─────────────────────────┴───────────┴─────────────────────────┴──────────────────┴────────────────────────┴───────────────────╯
```

You can also output to csv or json for further processing. It uses the `usageDetails` endpoint, which provided different type of data than the `query` endpoint.

## Filter

With the `--filter` option you can pass in one or more properties to filter on. 

```bash
azure-cost --filter "ResourceGroupName=yourresourcegroup;myresourcegroup" --filter "owner=me" 
```

In the example above, we look for resources in either `yourresourcegroup` or `myresourcegroup` and having a tag named `owner` with the value `me`.

Filters are passed along to the Cost API, so less data is retrieved. Compared to the query, where the data is queried with a JMESPATH expression and can be projected as well.

Multiple filters are combined with an `and` expression, while the values are split by the `;` and used as an `or`.

## Query

Use the `--query` to specify a [JMESPath](https://jmespath.org) expression. This allows you to filter the results. For example, to get the yesterday cost of the subscription, you can use the following query:

```bash
azure-cost -s 574385a9-08e9-49fe-91a2-27660d92b8f5 -o json --query "totals.yesterdayCost"
```

Or to list only the resource groups:

```bash
azure-cost -s 574385a9-08e9-49fe-91a2-27660d92b8f5 -o json --query "ByResourceGroup[*].[ResourceGroup, Cost]"
```

will output:

```json
[["mindbyte-sand-api",28.94824],["rg-test",16.457219149662315],["rg-weu",0.252499694771765],["cloud-storage-westeu",0.183537445632]]
```

For the JMESPath parsing, it uses the [JMESPath.Net](https://github.com/jdevillard/JmesPath.Net) library. Not all constructions might be implemented yet. If you find a query that does not work, please open an issue with a reproducable path at their repo.

## Timeframe

The default timeframe is the billing month to date. You can specify a custom timeframe using the `--from` and `--to` parameters and setting the `-t custom`. The timeframe is specified in the ISO 8601 format. 

Other options are:

- BillingMonthToDate
- Custom
- MonthToDate
- TheLastBillingMonth
- TheLastMonth
- WeekToDate

These options are based on the types exposed by the [query API](https://learn.microsoft.com/en-us/rest/api/cost-management/query/usage?tabs=HTTP#timeframetype).

## Output formats

The tool supports multiple output formats. The default is `Console` which will output the results to the console. You can specify a different format using the `--output` parameter. The different commands generate different outputs using the specified formatter. The following formats are supported:

### Console

The default output format. It will output the results to the console in a graphical way. For example the accumulated costs:

![](screenshot.png)


### Json / JsonC

The Json format is great for further processing of the data. It will output the results in a JSON format to the console. Using the > operator, you can redirect the output to a file. Use `jsonc` to get a colorized output.

```bash
azure-cost accumulatedCost -s 00000000-0000-0000-0000-000000000000 -o json > cost.json
```

```json
{
  "totals": {
    "todaysCost": 0.521266170218092,
    "yesterdayCost": 2.367501588413211,
    "lastSevenDaysCost": 17.089367673307038,
    "lastThirtyDaysCost": 30.887236456720686
  },
  "cost": [
    {
      "Date": "2023-04-01",
      "Cost": 2.365348403419757,
      "Currency": "EUR"
    },
    // snip
  ],
  "forecastedCosts": [
    {
      "Date": "2023-04-13",
      "Cost": 0,
      "Currency": "EUR"
    },
    // snip
  ],
  "byServiceNames": [
    {
      "ServiceName": "API Management",
      "Cost": 19.524664,
      "Currency": "EUR"
    },
    // snip
  ],
  "ByLocation": [
    {
      "Location": "EU West",
      "Cost": 30.68711937543843,
      "Currency": "EUR"
    },
    // snip
  ],
   "ByResourceGroup": [
    {
      "ResourceGroup": "rg-west-eu",
      "Cost": 30.68711937543843,
      "Currency": "EUR"
    },
    // snip
  ]
}
```

> **Tip**: Use the `--query` parameter here to manipulate the results, like filtering and projecting the data. Do keep in mind that it operates over the already fetched data.

### Text

A simple textual format. It will output the results in a simple text format.

```
Azure Cost Overview for 574385a9-08e9-49fe-91a2-27660d92b8f5 from 01/04/2023 to 14/04/2023                                                                    

Totals:
  Today: 0,52 EUR
  Yesterday: 2,37 EUR
  Last 7 days: 17,09 EUR
  Last 30 days: 30,89 EUR

By Service Name:
  API Management: 19,52 EUR
  Azure App Service: 5,32 EUR
  Azure Monitor: 3,67 EUR
  Container Registry: 2,06 EUR
  Log Analytics: 0,17 EUR
  Storage: 0,13 EUR
  Key Vault: 0,00 EUR
  Bandwidth: 0,00 EUR

By Location:
  EU West: 30,69 EUR
  Unknown: 0,20 EUR
  US West: 0,00 EUR
  US West 2: 0,00 EUR

```

Or when using the daily cost:

```
Daily Costs:
------------
Date        Cost (EUR) Breakdown
01/06/2023  50,47 Virtual Machines: 12,09 (23,95%), Azure DevOps: 10,63 (21,06%), Azure App Service: 7,67 (15,19%), Storage: 5,84 (11,58%), Functions: 4,75 (9,41%), IoT Hub: 1,51 (2,99%), API Management: 1,48 (2,93%), Azure Cosmos DB: 1,47 (2,91%), App Configuration: 1,12 (2,23%), Container Registry: 0,94 (1,85%), Other: 2,98 (5,90%)
02/06/2023  61,89 Virtual Machines: 20,08 (32,45%), Azure DevOps: 10,81 (17,47%), Storage: 8,69 (14,03%), Azure App Service: 7,67 (12,39%), Functions: 4,75 (7,67%), IoT Hub: 1,51 (2,44%), API Management: 1,48 (2,39%), Azure Cosmos DB: 1,47 (2,37%), App Configuration: 1,12 (1,81%), Container Registry: 0,94 (1,51%), Other: 3,38 (5,46%)
03/06/2023  62,14 Virtual Machines: 20,08 (32,31%), Azure DevOps: 10,99 (17,69%), Storage: 8,69 (13,98%), Azure App Service: 7,67 (12,34%), Functions: 4,75 (7,64%), IoT Hub: 1,51 (2,43%), API Management: 1,48 (2,38%), Azure Cosmos DB: 1,47 (2,36%), App Configuration: 1,12 (1,81%), Container Registry: 0,94 (1,51%), Other: 3,45 (5,55%)
04/06/2023  62,08 Virtual Machines: 20,08 (32,35%), Azure DevOps: 10,99 (17,70%), Storage: 8,69 (13,99%), Azure App Service: 7,67 (12,35%), Functions: 4,75 (7,65%), IoT Hub: 1,51 (2,43%), API Management: 1,48 (2,38%), Azure Cosmos DB: 1,47 (2,36%), App Configuration: 1,12 (1,81%), Container Registry: 0,94 (1,51%), Other: 3,39 (5,45%)
05/06/2023  63,99 Virtual Machines: 21,51 (33,62%), Azure DevOps: 10,99 (17,18%), Storage: 8,95 (13,98%), Azure App Service: 7,67 (11,98%), Functions: 4,75 (7,42%), IoT Hub: 1,51 (2,36%), API Management: 1,48 (2,31%), Azure Cosmos DB: 1,47 (2,29%), App Configuration: 1,12 (1,76%), Container Registry: 0,94 (1,46%), Other: 3,60 (5,63%)
06/06/2023  71,25 Virtual Machines: 23,36 (32,79%), Azure DevOps: 10,99 (15,43%), Storage: 9,30 (13,05%), Azure App Service: 7,67 (10,76%), Functions: 4,75 (6,66%), Azure Cognitive Search: 4,09 (5,74%), IoT Hub: 1,51 (2,12%), API Management: 1,48 (2,07%), Azure Cosmos DB: 1,47 (2,06%), Load Balancer: 1,13 (1,58%), Other: 5,51 (7,74%)
07/06/2023  65,62 Virtual Machines: 23,35 (35,58%), Azure DevOps: 11,53 (17,58%), Storage: 9,28 (14,14%), Azure App Service: 6,87 (10,47%), Functions: 4,75 (7,24%), IoT Hub: 1,51 (2,30%), API Management: 1,48 (2,25%), Azure Cosmos DB: 1,47 (2,24%), App Configuration: 1,12 (1,71%), Load Balancer: 1,08 (1,65%), Other: 3,18 (4,84%)
08/06/2023  57,04 Virtual Machines: 21,94 (38,47%), Azure DevOps: 11,72 (20,54%), Storage: 9,23 (16,18%), Functions: 3,56 (6,25%), Azure App Service: 2,61 (4,58%), IoT Hub: 1,51 (2,65%), API Management: 1,48 (2,59%), Azure Cosmos DB: 1,46 (2,56%), SQL Database: 0,89 (1,56%), Azure Database for PostgreSQL: 0,71 (1,25%), Other: 1,93 (3,38%)
09/06/2023  4,51 Virtual Machines: 1,86 (41,19%), Storage: 1,54 (34,08%), Azure App Service: 0,33 (7,24%), API Management: 0,18 (4,09%), Azure Cosmos DB: 0,18 (4,00%), Virtual Network: 0,10 (2,24%), Load Balancer: 0,09 (2,09%), Azure Database for PostgreSQL: 0,09 (1,97%), SQL Database: 0,06 (1,40%), Container Registry: 0,04 (0,86%), Other: 0,04 (0,83%)

```

### Csv

A CSV format. It will output the results in a CSV format which can be used in Excel or other tools. It will use the default culture of your system to format the numbers. If you combine the output of multiple runs into one file (like using the `>>` to append to a file), you can use the `--skipHeader` parameter to prevent the header from being written multiple times.

```bash
azure-cost accumulatedCost -s 574385a9-08e9-49fe-91a2-27660d92b8f5 -o csv > cost.csv
```

```csv
Date,Cost,CostUsd,Currencycost
01/05/2023,"2,366588585885843","2,563252097372957",EUR
02/05/2023,"2,36675103555328","2,563428046607759",EUR
03/05/2023,"2,366643737168579","2,563311831727289",EUR
04/05/2023,"2,366407861778791","2,563056355092609",EUR
05/05/2023,"2,367958990965315","2,564736383114534",EUR
06/05/2023,"1,904236728129323","2,06247880023687",EUR
07/05/2023,"2,36694351092248","2,613934066287242",EUR
08/05/2023,"0,521257579853968","0,575650808311731",EUR
```

### Markdown

A markdown format. It will output the results in a series of simple tables.

```bash
azure-cost accumulatedCost -s 574385a9-08e9-49fe-91a2-27660d92b8f5 -o markdown > cost.md
```

```markdown
# Azure Cost Overview

> Details for subscription id `574385a9-08e9-49fe-91a2-27660d92b8f5` from **01/04/2023** to **20/04/2023**

## Totals

|Period|Amount|
|---|---:|
|Today|0,00 EUR|
|Yesterday|1,27 EUR|
|Last 7 days|15,48 EUR|
|Last 30 days|45,84 EUR|

## By Service Name

|Service|Amount|
|---|---:|
|API Management|28,95 EUR|
|Azure App Service|7,89 EUR|
|Azure Monitor|5,51 EUR|
|Container Registry|3,06 EUR|
|Log Analytics|0,25 EUR|
|Storage|0,19 EUR|
|Key Vault|0,00 EUR|
|Bandwidth|0,00 EUR|

## By Location

|Location|Amount|
|---|---:|
|EU West|45,55 EUR|
|Unknown|0,30 EUR|
|US West|0,00 EUR|
|US North Central|0,00 EUR|
|US West 2|0,00 EUR|

## By Resource Group

|Resource Group|Amount|
|---|---:|
|mindbyte-sand-api|28,95 EUR|
|mindbyte-sand-azuremonitor|5,51 EUR|
|mindbyte-sand-registry|3,06 EUR|

<sup>Generated at 2023-04-21 07:40:23</sup>

```

Excluded in the above sample, but it will also include mermaidjs diagrams as well.

## Error handling

When the tool detects an exception, it will write this to the error stream and return with a -1. This can be used in scripts to detect if something went wrong. If you output using one of the formatters and want to save this to a file, you can use the `>` operator to redirect the output to a file. If you want to capture any errors as well, you can use the `2>&1` operator to redirect the error stream to the output stream. Or output it to a different file.

```bash 
azure-cost accumulatedCost -o markdown > cost.md 2>error.log
```  

> Breaking change in version 0.41.0: The tool will now return with a -1 when an error occurs. This is to make it easier to detect errors in scripts.

## Iterate over multiple subscriptions

Since the tool operates on a single subscription only, you will need to loop over multiple subscriptions yourself. You can do this by using the `az account list` command and then using the `--subscription` parameter to switch between subscriptions.

```bash
az account list --query "[].id" -o tsv | while read -r id; do
    echo "Subscription: $id"
    azure-cost accumulatedCost -o markdown -s $id
done
```

Or using Powershell

```powershell
az account list --query "[].id" -o tsv | ForEach-Object {
    Write-Host "Subscription: $_"
    azure-cost accumulatedCost -o markdown -s $_
}
```

Or this snippet by [@EEN421](https://github.com/EEN421) to get the cost by resource for each subscription and append it to a single CSV file.

```powershell
#Connect to Azure
Connect-AzAccount -UseDeviceAuthentication -Tenant xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxx

#Pull list of Subscriptions
$ids = Get-AzSubscription -TenantId xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxx| Select-Object id,name
$firstid = $ids | Select-Object -first 1
$remainingids = $ids | Select-Object -skip 1

#Create CSV for first subscription with header
$firstoutput = azure-cost costByResource -s $firstid.id -t Custom --from 2023-05-01 --to 2023-05-31 -o csv
Add-Content ".\report.csv" $firstoutput

#Loop through remaining subscriptions and append to CSV
Foreach ($id in $remainingids) {
$output = azure-cost costByResource -s($id.id) -t Custom --from 2023-05-01 --to 2023-05-31 -o csv --skipHeader
Add-Content ".\report.csv" $output
}
```

Using the `--skipHeader` parameter is important here, otherwise you will get a header for each subscription which will mess up the output file as it will append the data to the same file.

## Is there cost involved?

No, the calls to the Azure Cost APIs are [free](https://learn.microsoft.com/en-us/azure/cost-management-billing/automate/automation-faq#am-i-charged-for-using-the-cost-details-api), although there are some [rate limits](https://learn.microsoft.com/en-us/azure/cost-management-billing/automate/get-small-usage-datasets-on-demand#latency-and-rate-limits) in place. Avoid pulling data too often as it will only be refreshed every [4 hours](https://learn.microsoft.com/en-us/azure/cost-management-billing/automate/get-small-usage-datasets-on-demand#request-schedule).

## Let's Connect!

I appreciate every star ⭐ that my projects receive, and your support means a lot to me! If you find my projects useful or enjoyable, please consider giving them a star.

For inquiries, suggestions, or contributions, feel free to open an issue or a pull request. You can also reach out to me directly via [LinkedIn](https://www.linkedin.com/in/michielvanoudheusden/).

![Alt](https://repobeats.axiom.co/api/embed/a5c7b68fe50da70986f5cb386be13c0e496f9e15.svg "Repobeats analytics image")
