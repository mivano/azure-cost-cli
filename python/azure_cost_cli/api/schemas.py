"""Pydantic schemas for request query parameters and JSON responses."""
from __future__ import annotations

from datetime import date
from typing import Any, Optional

from pydantic import BaseModel, Field


# ---------------------------------------------------------------------------
# Shared query-param schemas (used as FastAPI Depends)
# ---------------------------------------------------------------------------

class CommonParams(BaseModel):
    """Query parameters shared by every cost endpoint."""

    subscription: Optional[str] = Field(None, description="Azure subscription ID")
    resource_group: Optional[str] = Field(None, alias="resourceGroup", description="Resource group scope")
    billing_account: Optional[str] = Field(None, alias="billingAccount")
    enrollment_account: Optional[str] = Field(None, alias="enrollmentAccount")

    timeframe: str = Field("BillingMonthToDate", description=(
        "BillingMonthToDate | Custom | MonthToDate | "
        "TheLastBillingMonth | TheLastMonth | WeekToDate"
    ))
    from_date: Optional[date] = Field(None, alias="from", description="Start date (YYYY-MM-DD)")
    to_date: Optional[date] = Field(None, alias="to", description="End date (YYYY-MM-DD)")
    metric: str = Field("ActualCost", description="ActualCost | AmortizedCost")
    filters: list[str] = Field(
        default_factory=list,
        description="Dimension/tag filters e.g. ResourceGroupName=rg1",
    )
    use_usd: bool = Field(False, alias="useUsd")

    model_config = {"populate_by_name": True}


# ---------------------------------------------------------------------------
# Response schemas
# ---------------------------------------------------------------------------

class CostItemResponse(BaseModel):
    date: date
    cost: float
    cost_usd: float = Field(alias="costUsd")
    currency: str

    model_config = {"populate_by_name": True}


class CostNamedItemResponse(BaseModel):
    name: str
    cost: float
    cost_usd: float = Field(alias="costUsd")
    currency: str

    model_config = {"populate_by_name": True}


class AccumulatedTotals(BaseModel):
    todays_cost: float = Field(alias="todaysCost")
    yesterday_cost: float = Field(alias="yesterdayCost")
    last_seven_days_cost: float = Field(alias="lastSevenDaysCost")
    last_thirty_days_cost: float = Field(alias="lastThirtyDaysCost")
    total_cost_in_timeframe: float = Field(alias="totalCostInTimeframe")

    model_config = {"populate_by_name": True}


class AccumulatedCostResponse(BaseModel):
    totals: AccumulatedTotals
    costs: list[CostItemResponse]
    forecasted_costs: list[CostItemResponse] = Field(alias="forecastedCosts")
    by_service_names: list[CostNamedItemResponse] = Field(alias="byServiceNames")
    by_location: list[CostNamedItemResponse] = Field(alias="byLocation")
    by_resource_group: list[CostNamedItemResponse] = Field(alias="byResourceGroup")
    by_subscription: Optional[list[CostNamedItemResponse]] = Field(None, alias="bySubscription")

    model_config = {"populate_by_name": True}


class DailyCostItem(BaseModel):
    date: date
    name: str
    cost: float
    cost_usd: float = Field(alias="costUsd")
    currency: str
    tags: Optional[dict[str, str]] = None

    model_config = {"populate_by_name": True}


class DailyCostGroup(BaseModel):
    date: date
    items: list[DailyCostItem]


class ResourceItem(BaseModel):
    resource_id: str = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    resource_type: str = Field(alias="resourceType")
    resource_location: str = Field(alias="resourceLocation")
    resource_group_name: str = Field(alias="resourceGroupName")
    charge_type: str = Field(alias="chargeType")
    publisher_type: str = Field(alias="publisherType")
    service_name: Optional[str] = Field(None, alias="serviceName")
    service_tier: Optional[str] = Field(None, alias="serviceTier")
    meter: Optional[str] = None
    cost: float
    cost_usd: float = Field(alias="costUsd")
    currency: str
    tags: dict[str, str] = Field(default_factory=dict)

    model_config = {"populate_by_name": True}


class CostByResourceResponse(BaseModel):
    total_count: int = Field(alias="totalCount")
    total_cost: float = Field(alias="totalCost")
    currency: str
    resources: list[ResourceItem]

    model_config = {"populate_by_name": True}


class TagValueSummary(BaseModel):
    value: str
    cost: float
    currency: str


class TagGroupResponse(BaseModel):
    tag: str
    values: list[TagValueSummary]


class BudgetNotification(BaseModel):
    name: str
    enabled: bool
    operator: str
    threshold: float
    contact_emails: list[str] = Field(alias="contactEmails", default_factory=list)

    model_config = {"populate_by_name": True}


class BudgetResponse(BaseModel):
    name: str
    id: str
    amount: float
    time_grain: str = Field(alias="timeGrain")
    start_date: str = Field(alias="startDate")
    end_date: str = Field(alias="endDate")
    current_spend_amount: Optional[float] = Field(None, alias="currentSpendAmount")
    current_spend_currency: Optional[str] = Field(None, alias="currentSpendCurrency")
    current_spend_percentage: Optional[float] = Field(None, alias="currentSpendPercentage")
    forecast_amount: Optional[float] = Field(None, alias="forecastAmount")
    forecast_currency: Optional[str] = Field(None, alias="forecastCurrency")
    notifications: list[BudgetNotification] = Field(default_factory=list)

    model_config = {"populate_by_name": True}


class AnomalyResponse(BaseModel):
    name: str
    detection_date: date = Field(alias="detectionDate")
    message: str
    cost_difference: float = Field(alias="costDifference")
    anomaly_type: str = Field(alias="anomalyType")

    model_config = {"populate_by_name": True}


class RegionResponse(BaseModel):
    id: str
    display_name: str = Field(alias="displayName")
    continent: str
    location: str
    latitude: float
    longitude: float
    is_open: bool = Field(alias="isOpen")

    model_config = {"populate_by_name": True}


class PriceItemResponse(BaseModel):
    region: str
    location: str
    retail_price: float = Field(alias="retailPrice")
    unit_price: float = Field(alias="unitPrice")
    currency_code: str = Field(alias="currencyCode")
    unit_of_measure: str = Field(alias="unitOfMeasure")
    sku_name: str = Field(alias="skuName")

    model_config = {"populate_by_name": True}


class WhatIfRegionResourceResponse(BaseModel):
    resource_id: str = Field(alias="resourceId")
    resource_name: str = Field(alias="resourceName")
    prices: list[PriceItemResponse]

    model_config = {"populate_by_name": True}


class DevTestItemResponse(BaseModel):
    resource_name: str = Field(alias="resourceName")
    resource_group: str = Field(alias="resourceGroup")
    product: str
    meter_name: str = Field(alias="meterName")
    region: str
    currency: str
    unit_of_measure: str = Field(alias="unitOfMeasure")
    quantity: float
    current_unit_price: float = Field(alias="currentUnitPrice")
    current_cost: float = Field(alias="currentCost")
    dev_test_unit_price: Optional[float] = Field(None, alias="devTestUnitPrice")
    dev_test_cost: Optional[float] = Field(None, alias="devTestCost")
    savings: Optional[float] = None
    savings_percentage: Optional[float] = Field(None, alias="savingsPercentage")

    model_config = {"populate_by_name": True}


class DiffCostResponse(BaseModel):
    costs: list[CostItemResponse]
    forecasted_costs: list[CostItemResponse] = Field(alias="forecastedCosts")
    by_service_names: list[CostNamedItemResponse] = Field(alias="byServiceNames")
    by_location: list[CostNamedItemResponse] = Field(alias="byLocation")
    by_resource_group: list[CostNamedItemResponse] = Field(alias="byResourceGroup")

    model_config = {"populate_by_name": True}


class HealthResponse(BaseModel):
    status: str = "ok"
    version: str = "1.0.0"
