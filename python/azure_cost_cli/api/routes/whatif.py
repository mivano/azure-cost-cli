"""GET /what-if/region and GET /what-if/devtest – mirror the what-if CLI commands."""
from __future__ import annotations

from datetime import date
from typing import Annotated, Optional

from fastapi import APIRouter, HTTPException, Query

from azure_cost_cli.api.dependencies import (
    get_from_date,
    get_scope,
    get_resolved_subscription,
    get_to_date,
    make_retriever,
)
from azure_cost_cli.api.schemas import DevTestItemResponse, PriceItemResponse, WhatIfRegionResourceResponse
from azure_cost_cli.price_api import AzurePriceRetriever

router = APIRouter(prefix="/what-if", tags=["What-If"])

_COMMON = dict(
    subscription=(Annotated[Optional[str], Query(description="Azure subscription ID")], None),
    resource_group=(Annotated[Optional[str], Query(alias="resourceGroup")], None),
    billing_account=(Annotated[Optional[str], Query(alias="billingAccount")], None),
    enrollment_account=(Annotated[Optional[str], Query(alias="enrollmentAccount")], None),
)


@router.get(
    "/region",
    response_model=list[WhatIfRegionResourceResponse],
    summary="What-if: VM cost comparison across regions",
)
async def get_whatif_region(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    from_date: Annotated[Optional[date], Query(alias="from")] = None,
    to_date: Annotated[Optional[date], Query(alias="to")] = None,
    cost_api_base_address: Annotated[str, Query(alias="costApiBaseAddress")] = "https://management.azure.com/",
    price_api_base_address: Annotated[str, Query(alias="priceApiBaseAddress")] = "https://prices.azure.com/",
    http_timeout: Annotated[int, Query(alias="httpTimeout")] = 100,
    debug: bool = False,
):
    scope = get_scope(subscription, resource_group, billing_account, enrollment_account)
    resolved_sub = get_resolved_subscription(subscription, resource_group, billing_account, enrollment_account)
    scope = get_scope(resolved_sub, resource_group, billing_account, enrollment_account)

    from_d = get_from_date(from_date)
    to_d = get_to_date(to_date)

    retriever = make_retriever(cost_api_base_address, http_timeout, debug)
    price_retriever = AzurePriceRetriever(price_api_address=price_api_base_address, http_timeout=http_timeout)

    try:
        usage_details = await retriever.retrieve_usage_details(scope, "", from_d, to_d)

        vm_resources: dict = {}
        for u in usage_details:
            if (
                u.properties
                and u.properties.consumed_service == "Microsoft.Compute"
                and u.properties.meter_details
                and u.properties.meter_details.meter_category == "Virtual Machines"
            ):
                rid = u.properties.resource_id
                if rid not in vm_resources:
                    vm_resources[rid] = u
                else:
                    vm_resources[rid].properties.quantity += u.properties.quantity
                    vm_resources[rid].properties.cost += u.properties.cost

        price_cache: dict = {}
        result: list[WhatIfRegionResourceResponse] = []

        for usage in vm_resources.values():
            sku_name = usage.properties.meter_details.meter_name if usage.properties.meter_details else ""
            meter_id = usage.properties.meter_id
            currency = usage.properties.billing_currency

            cache_key = f"{sku_name}:{meter_id}:{currency}"
            if cache_key not in price_cache:
                filter_str = (
                    f"serviceName eq 'Virtual Machines' and skuName eq '{sku_name}' and type eq 'Consumption'"
                )
                prices = await price_retriever.get_azure_prices(currency, filter_str)
                actual = next((p for p in prices if p.meter_id == meter_id), None)
                if actual:
                    prices = [p for p in prices if p.product_name == actual.product_name]
                price_cache[cache_key] = prices

            prices_for_vm = price_cache[cache_key]
            result.append(
                WhatIfRegionResourceResponse(
                    resourceId=usage.properties.resource_id,
                    resourceName=usage.properties.resource_name,
                    prices=[
                        PriceItemResponse(
                            region=p.arm_region_name,
                            location=p.location,
                            retailPrice=p.retail_price,
                            unitPrice=p.unit_price,
                            currencyCode=p.currency_code,
                            unitOfMeasure=p.unit_of_measure,
                            skuName=p.sku_name,
                        )
                        for p in prices_for_vm
                    ],
                )
            )
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()
        await price_retriever.close()

    return result


@router.get(
    "/devtest",
    response_model=list[DevTestItemResponse],
    summary="What-if: Dev/Test pricing comparison for VMs",
)
async def get_whatif_devtest(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    from_date: Annotated[Optional[date], Query(alias="from")] = None,
    to_date: Annotated[Optional[date], Query(alias="to")] = None,
    cost_api_base_address: Annotated[str, Query(alias="costApiBaseAddress")] = "https://management.azure.com/",
    price_api_base_address: Annotated[str, Query(alias="priceApiBaseAddress")] = "https://prices.azure.com/",
    http_timeout: Annotated[int, Query(alias="httpTimeout")] = 100,
    debug: bool = False,
):
    scope = get_scope(subscription, resource_group, billing_account, enrollment_account)
    resolved_sub = get_resolved_subscription(subscription, resource_group, billing_account, enrollment_account)
    scope = get_scope(resolved_sub, resource_group, billing_account, enrollment_account)

    from_d = get_from_date(from_date)
    to_d = get_to_date(to_date)

    retriever = make_retriever(cost_api_base_address, http_timeout, debug)
    price_retriever = AzurePriceRetriever(price_api_address=price_api_base_address, http_timeout=http_timeout)

    try:
        usage_details = await retriever.retrieve_usage_details(scope, "", from_d, to_d)

        items: list[DevTestItemResponse] = []
        for u in usage_details:
            if not (
                u.properties
                and u.properties.consumed_service == "Microsoft.Compute"
                and u.properties.meter_details
                and u.properties.meter_details.meter_category == "Virtual Machines"
            ):
                continue

            sku_name = u.properties.meter_details.meter_name
            currency = u.properties.billing_currency
            filter_str = (
                f"serviceName eq 'Virtual Machines' and skuName eq '{sku_name}' and priceType eq 'DevTestConsumption'"
            )
            devtest_prices = await price_retriever.get_azure_prices(currency, filter_str)
            devtest_price = next(
                (p for p in devtest_prices if p.arm_region_name == u.properties.resource_location), None
            )

            dev_unit_price = devtest_price.unit_price if devtest_price else None
            dev_cost = dev_unit_price * u.properties.quantity if dev_unit_price is not None else None
            savings = (u.properties.cost - dev_cost) if dev_cost is not None else None
            savings_pct = (savings / u.properties.cost * 100) if savings and u.properties.cost else None

            items.append(
                DevTestItemResponse(
                    resourceName=u.properties.resource_name,
                    resourceGroup=u.properties.resource_group,
                    product=u.properties.product,
                    meterName=sku_name,
                    region=u.properties.resource_location,
                    currency=currency,
                    unitOfMeasure=u.properties.meter_details.unit_of_measure,
                    quantity=u.properties.quantity,
                    currentUnitPrice=u.properties.unit_price,
                    currentCost=u.properties.cost,
                    devTestUnitPrice=dev_unit_price,
                    devTestCost=dev_cost,
                    savings=savings,
                    savingsPercentage=savings_pct,
                )
            )
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()
        await price_retriever.close()

    return items
