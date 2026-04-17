"""Data models mirroring the .NET records/classes in CostApi/."""
from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, datetime
from typing import Optional


# ---------------------------------------------------------------------------
# Subscription
# ---------------------------------------------------------------------------

@dataclass
class SubscriptionPolicies:
    locationPlacementId: str = ""
    quotaId: str = ""
    spendingLimit: str = ""


@dataclass
class Subscription:
    id: str = ""
    authorizationSource: str = ""
    managedByTenants: list = field(default_factory=list)
    subscriptionId: str = ""
    tenantId: str = ""
    displayName: str = ""
    state: str = ""
    subscriptionPolicies: Optional[SubscriptionPolicies] = None


# ---------------------------------------------------------------------------
# Cost items
# ---------------------------------------------------------------------------

@dataclass
class CostItem:
    date: date
    cost: float
    cost_usd: float
    currency: str


@dataclass
class CostNamedItem:
    item_name: str
    cost: float
    cost_usd: float
    currency: str


@dataclass
class CostDailyItem:
    date: date
    name: str
    cost: float
    cost_usd: float
    currency: str
    tags: Optional[dict[str, str]] = None


@dataclass
class CostResourceItem:
    cost: float
    cost_usd: float
    resource_id: str
    resource_type: str
    resource_location: str
    charge_type: str
    resource_group_name: str
    publisher_type: str
    service_name: Optional[str]
    service_tier: Optional[str]
    meter: Optional[str]
    tags: dict[str, str] = field(default_factory=dict)
    currency: str = "USD"

    @property
    def resource_name(self) -> str:
        parts = (self.resource_id or "").split("/")
        return parts[-1] if parts else ""


# ---------------------------------------------------------------------------
# Budget
# ---------------------------------------------------------------------------

@dataclass
class Notification:
    name: str
    enabled: bool
    operator: str
    threshold: float
    contact_emails: list[str] = field(default_factory=list)
    contact_roles: list[str] = field(default_factory=list)
    contact_groups: list[str] = field(default_factory=list)


@dataclass
class BudgetItem:
    name: str
    id: str
    amount: float
    time_grain: str
    start_date: datetime
    end_date: datetime
    current_spend_amount: Optional[float]
    current_spend_currency: Optional[str]
    forecast_amount: Optional[float]
    forecast_currency: Optional[str]
    notifications: list[Notification] = field(default_factory=list)


# ---------------------------------------------------------------------------
# Accumulated cost (aggregate passed to formatters)
# ---------------------------------------------------------------------------

@dataclass
class AccumulatedCostDetails:
    subscription: Optional[Subscription]
    enrollment_account: Optional[object]
    costs: list[CostItem]
    forecasted_costs: list[CostItem]
    by_service_name_costs: list[CostNamedItem]
    by_location_costs: list[CostNamedItem]
    by_resource_group_costs: list[CostNamedItem]
    by_subscription_costs: Optional[list[CostNamedItem]] = None


# ---------------------------------------------------------------------------
# Anomaly detection
# ---------------------------------------------------------------------------

from enum import Enum


class AnomalyType(str, Enum):
    NEW_COST = "NewCost"
    REMOVED_COST = "RemovedCost"
    SIGNIFICANT_CHANGE = "SignificantChange"
    STEADY_GROWTH = "SteadyGrowth"


@dataclass
class AnomalyDetectionResult:
    name: str
    detection_date: date
    message: str
    cost_difference: float
    anomaly_type: AnomalyType
    data: list[CostDailyItem] = field(default_factory=list)


# ---------------------------------------------------------------------------
# Regions
# ---------------------------------------------------------------------------

@dataclass
class AzureRegion:
    id: str = ""
    continent: str = ""
    geography_id: str = ""
    display_name: str = ""
    location: str = ""
    latitude: float = 0.0
    longitude: float = 0.0
    type_id: str = ""
    is_open: bool = False
    year_open: Optional[int] = None
    compliance_ids: list[str] = field(default_factory=list)
    has_ground_station: bool = False
    data_residency: str = ""
    available_to: str = ""
    availability_zones_id: str = ""
    availability_zones_nearest_region_ids: list[str] = field(default_factory=list)
    products_by_region_link: str = ""
    products_by_region_link_non_regional: str = ""
    sustainability_ids: list[str] = field(default_factory=list)
    disaster_recovery_crossregion_ids: list[str] = field(default_factory=list)
    disaster_recovery_inregion_ids: list[str] = field(default_factory=list)


# ---------------------------------------------------------------------------
# Pricing / What-If
# ---------------------------------------------------------------------------

@dataclass
class PriceRecord:
    currency_code: str = ""
    tier_minimum_units: float = 0.0
    retail_price: float = 0.0
    unit_price: float = 0.0
    arm_region_name: str = ""
    location: str = ""
    effective_start_date: str = ""
    meter_id: str = ""
    meter_name: str = ""
    product_id: str = ""
    sku_id: str = ""
    product_name: str = ""
    sku_name: str = ""
    service_name: str = ""
    service_id: str = ""
    service_family: str = ""
    unit_of_measure: str = ""
    type: str = ""
    is_primary_meter_region: bool = False
    arm_sku_name: str = ""


@dataclass
class UsageDetails:
    kind: str = ""
    id: str = ""
    name: str = ""
    type: str = ""
    tags: dict[str, str] = field(default_factory=dict)
    properties: Optional["UsageProperties"] = None


@dataclass
class MeterDetails:
    meter_category: str = ""
    unit_of_measure: str = ""
    meter_name: str = ""
    meter_sub_category: str = ""


@dataclass
class UsageProperties:
    billing_period_start_date: str = ""
    billing_period_end_date: str = ""
    billing_profile_id: str = ""
    billing_profile_name: str = ""
    subscription_id: str = ""
    subscription_name: str = ""
    date: str = ""
    product: str = ""
    meter_id: str = ""
    quantity: float = 0.0
    effective_price: float = 0.0
    cost: float = 0.0
    unit_price: float = 0.0
    billing_currency: str = ""
    resource_location: str = ""
    consumed_service: str = ""
    resource_id: str = ""
    resource_name: str = ""
    additional_info: str = ""
    resource_group: str = ""
    meter_details: Optional[MeterDetails] = None
    charge_type: str = ""
    frequency: str = ""
    publisher_type: str = ""
    is_azure_credit_eligible: bool = False
    offer_id: str = ""


@dataclass
class DevTestComparisonItem:
    resource_name: str
    resource_group: str
    product: str
    meter_name: str
    region: str
    currency: str
    unit_of_measure: str
    quantity: float
    current_unit_price: float
    current_cost: float
    dev_test_unit_price: Optional[float]
    dev_test_cost: Optional[float]
    savings: Optional[float]
    savings_percentage: Optional[float]


# ---------------------------------------------------------------------------
# Scope
# ---------------------------------------------------------------------------

class Scope:
    def __init__(self, name: str, scope_path: str, is_subscription_based: bool):
        self.name = name
        self.scope_path = scope_path
        self.is_subscription_based = is_subscription_based

    @staticmethod
    def subscription(subscription_id: str) -> "Scope":
        return Scope("Subscription", f"/subscriptions/{subscription_id}", True)

    @staticmethod
    def resource_group(subscription_id: str, resource_group: str) -> "Scope":
        return Scope(
            "ResourceGroup",
            f"/subscriptions/{subscription_id}/resourceGroups/{resource_group}",
            True,
        )

    @staticmethod
    def enrollment_account(billing_account_id: str, enrollment_account_id: str) -> "Scope":
        return Scope(
            "EnrollmentAccount",
            f"/providers/Microsoft.Billing/billingAccounts/{billing_account_id}"
            f"/enrollmentAccounts/{enrollment_account_id}",
            False,
        )

    @staticmethod
    def billing_account(billing_account_id: str) -> "Scope":
        return Scope(
            "BillingAccount",
            f"/providers/Microsoft.Billing/billingAccounts/{billing_account_id}",
            False,
        )


# ---------------------------------------------------------------------------
# Enums
# ---------------------------------------------------------------------------

class TimeframeType(str, Enum):
    BILLING_MONTH_TO_DATE = "BillingMonthToDate"
    CUSTOM = "Custom"
    MONTH_TO_DATE = "MonthToDate"
    THE_LAST_BILLING_MONTH = "TheLastBillingMonth"
    THE_LAST_MONTH = "TheLastMonth"
    WEEK_TO_DATE = "WeekToDate"


class MetricType(str, Enum):
    ACTUAL_COST = "ActualCost"
    AMORTIZED_COST = "AmortizedCost"


class OutputFormat(str, Enum):
    CONSOLE = "Console"
    JSON = "Json"
    JSONC = "JsonC"
    TEXT = "Text"
    MARKDOWN = "Markdown"
    CSV = "Csv"
