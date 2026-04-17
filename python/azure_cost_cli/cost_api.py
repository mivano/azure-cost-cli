"""Azure Cost Management API retriever – mirrors AzureCostApiRetriever.cs."""
from __future__ import annotations

import json
import sys
from datetime import date, datetime, timedelta, timezone
from typing import Any, Optional

import httpx
from azure.identity import AzureCliCredential, ChainedTokenCredential, DefaultAzureCredential

from azure_cost_cli.models import (
    AnomalyDetectionResult,
    BudgetItem,
    CostDailyItem,
    CostItem,
    CostNamedItem,
    CostResourceItem,
    MeterDetails,
    MetricType,
    Notification,
    Scope,
    Subscription,
    SubscriptionPolicies,
    TimeframeType,
    UsageDetails,
    UsageProperties,
)

_DIMENSION_NAMES = {
    "PublisherType", "ResourceGroupName", "ResourceLocation", "ResourceId",
    "ServiceName", "ServiceTier", "ServiceFamily", "InvoiceId", "CustomerName",
    "PartnerName", "ResourceType", "ChargeType", "BillingPeriod",
    "MeterCategory", "MeterSubCategory",
}


def _today_utc() -> date:
    return datetime.now(timezone.utc).date()


def resolve_timeframe(
    timeframe: TimeframeType, from_date: date, to_date: date, today: date
) -> tuple[TimeframeType, date, date]:
    """Translate deprecated timeframe values into Custom ranges."""
    if timeframe in (TimeframeType.THE_LAST_MONTH, TimeframeType.THE_LAST_BILLING_MONTH):
        first_of_month = today.replace(day=1)
        last_month_start = (first_of_month - timedelta(days=1)).replace(day=1)
        last_month_end = first_of_month - timedelta(days=1)
        return TimeframeType.CUSTOM, last_month_start, last_month_end
    return timeframe, from_date, to_date


def _generate_filters(filter_args: list[str] | None) -> Any:
    if not filter_args:
        return None

    filters = []
    for arg in filter_args:
        name, values_raw = arg.split("=", 1)
        values = values_raw.split(";")
        filter_dict = {"Name": name, "Operator": "In", "Values": values}
        if name in _DIMENSION_NAMES:
            filters.append({"Dimensions": filter_dict})
        else:
            filters.append({"Tags": filter_dict})

    if len(filters) > 1:
        return {"And": filters}
    return filters[0]


class AzureCostApiRetriever:
    """Calls the Azure Cost Management REST API."""

    def __init__(
        self,
        cost_api_address: str = "https://management.azure.com/",
        http_timeout: int = 100,
        debug: bool = False,
    ):
        self.cost_api_address = cost_api_address.rstrip("/") + "/"
        self.http_timeout = http_timeout
        self.debug = debug
        self._token: str | None = None
        self._client: httpx.AsyncClient | None = None

    async def _ensure_client(self) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(
                base_url=self.cost_api_address,
                timeout=self.http_timeout,
                follow_redirects=True,
            )
        return self._client

    async def _get_token(self) -> str:
        if self._token:
            return self._token
        credential = ChainedTokenCredential(
            AzureCliCredential(),
            DefaultAzureCredential(),
        )
        scope = self.cost_api_address + ".default"
        token = credential.get_token(scope)
        if self.debug:
            print(f"[debug] Token retrieved, expires at: {token.expires_on}", file=sys.stderr)
        self._token = token.token
        return self._token

    def _build_url(self, scope: Scope, path: str) -> str:
        return scope.scope_path.lstrip("/") + path

    async def _paged_post(self, scope: Scope, path: str, payload: dict) -> list[Any]:
        """POST to the cost API and follow nextLink pagination.  Returns all rows."""
        client = await self._ensure_client()
        token = await self._get_token()
        headers = {"Authorization": f"Bearer {token}"}

        url: str | None = self._build_url(scope, path)
        all_rows: list[Any] = []

        while url:
            if self.debug:
                print(f"[debug] POST {url}", file=sys.stderr)
                print(json.dumps(payload, indent=2, default=str), file=sys.stderr)

            resp = client.build_request("POST", url, json=payload, headers=headers)
            response = await client.send(resp)
            response.raise_for_status()
            data = response.json()

            props = data.get("properties", {})
            rows = props.get("rows", [])
            all_rows.extend(rows)

            next_link = props.get("nextLink")
            url = next_link if next_link else None

        return all_rows

    async def _get(self, path: str) -> dict:
        client = await self._ensure_client()
        token = await self._get_token()
        headers = {"Authorization": f"Bearer {token}"}
        if self.debug:
            print(f"[debug] GET {path}", file=sys.stderr)
        response = await client.get(path, headers=headers)
        response.raise_for_status()
        return response.json()

    async def _scope_get(self, scope: Scope, path: str) -> dict:
        return await self._get(self._build_url(scope, path))

    # ------------------------------------------------------------------
    # Subscription
    # ------------------------------------------------------------------

    async def retrieve_subscription(self, subscription_id: str) -> Subscription:
        data = await self._get(f"subscriptions/{subscription_id}/?api-version=2019-11-01")
        policies = data.get("subscriptionPolicies") or {}
        return Subscription(
            id=data.get("id", ""),
            authorizationSource=data.get("authorizationSource", ""),
            managedByTenants=data.get("managedByTenants", []),
            subscriptionId=data.get("subscriptionId", ""),
            tenantId=data.get("tenantId", ""),
            displayName=data.get("displayName", ""),
            state=data.get("state", ""),
            subscriptionPolicies=SubscriptionPolicies(
                locationPlacementId=policies.get("locationPlacementId", ""),
                quotaId=policies.get("quotaId", ""),
                spendingLimit=policies.get("spendingLimit", ""),
            ),
        )

    # ------------------------------------------------------------------
    # Costs
    # ------------------------------------------------------------------

    def _time_period(self, resolved: tuple) -> dict | None:
        tf, from_date, to_date = resolved
        if tf == TimeframeType.CUSTOM:
            return {"from": from_date.strftime("%Y-%m-%d"), "to": to_date.strftime("%Y-%m-%d")}
        return None

    async def retrieve_costs(
        self,
        scope: Scope,
        filter_args: list[str],
        metric: MetricType,
        timeframe: TimeframeType,
        from_date: date,
        to_date: date,
    ) -> list[CostItem]:
        resolved = resolve_timeframe(timeframe, from_date, to_date, _today_utc())
        payload = {
            "type": metric.value,
            "timeframe": resolved[0].value,
            "dataSet": {
                "granularity": "Daily",
                "aggregation": {
                    "totalCost": {"name": "Cost", "function": "Sum"},
                    "totalCostUSD": {"name": "CostUSD", "function": "Sum"},
                },
                "sorting": [{"direction": "Ascending", "name": "UsageDate"}],
                "filter": _generate_filters(filter_args),
            },
        }
        tp = self._time_period(resolved)
        if tp:
            payload["timePeriod"] = tp

        path = "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000"
        rows = await self._paged_post(scope, path, payload)

        items = []
        for row in rows:
            d = datetime.strptime(str(row[2]), "%Y%m%d").date()
            items.append(CostItem(
                date=d,
                cost=float(row[0]),
                cost_usd=float(row[1]),
                currency=str(row[3]),
            ))
        return items

    async def retrieve_forecasted_costs(
        self,
        scope: Scope,
        filter_args: list[str],
        metric: MetricType,
        timeframe: TimeframeType,
        from_date: date,
        to_date: date,
    ) -> list[CostItem]:
        resolved = resolve_timeframe(timeframe, from_date, to_date, _today_utc())
        payload = {
            "type": metric.value,
            "timeframe": resolved[0].value,
            "dataSet": {
                "granularity": "Daily",
                "aggregation": {"totalCost": {"name": "Cost", "function": "Sum"}},
                "filter": _generate_filters(filter_args),
                "sorting": [{"direction": "ascending", "name": "UsageDate"}],
            },
        }
        tp = self._time_period(resolved)
        if tp:
            payload["timePeriod"] = tp

        path = "/providers/Microsoft.CostManagement/forecast?api-version=2021-10-01&$top=5000"
        try:
            rows = await self._paged_post(scope, path, payload)
        except Exception as exc:
            if self.debug:
                print(f"[debug] Forecast call failed (expected for some subscriptions): {exc}", file=sys.stderr)
            return []

        items = []
        for row in rows:
            d = datetime.strptime(str(row[1]), "%Y%m%d").date()
            currency = str(row[3]) if len(row) > 3 else "USD"
            items.append(CostItem(date=d, cost=float(row[0]), cost_usd=float(row[0]), currency=currency))
        return items

    async def _retrieve_cost_by_dimension(
        self,
        scope: Scope,
        filter_args: list[str],
        metric: MetricType,
        timeframe: TimeframeType,
        from_date: date,
        to_date: date,
        dimension_name: str,
        name_col: int = 2,
        currency_col: int = 3,
    ) -> list[CostNamedItem]:
        resolved = resolve_timeframe(timeframe, from_date, to_date, _today_utc())
        payload = {
            "type": metric.value,
            "timeframe": resolved[0].value,
            "dataSet": {
                "granularity": "None",
                "aggregation": {
                    "totalCost": {"name": "Cost", "function": "Sum"},
                    "totalCostUSD": {"name": "CostUSD", "function": "Sum"},
                },
                "sorting": [{"direction": "Ascending", "name": "UsageDate"}],
                "grouping": [
                    {"type": "Dimension", "name": dimension_name},
                ],
                "filter": _generate_filters(filter_args),
            },
        }
        # ResourceGroupName grouping also includes ChargeType
        if dimension_name in ("ResourceGroupName", "SubscriptionName"):
            payload["dataSet"]["grouping"].append({"type": "Dimension", "name": "ChargeType"})
            currency_col = 4

        tp = self._time_period(resolved)
        if tp:
            payload["timePeriod"] = tp

        path = "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000"
        rows = await self._paged_post(scope, path, payload)

        items = []
        for row in rows:
            items.append(CostNamedItem(
                item_name=str(row[name_col]),
                cost=float(row[0]),
                cost_usd=float(row[1]),
                currency=str(row[currency_col]),
            ))
        return items

    async def retrieve_cost_by_service_name(
        self, scope: Scope, filter_args: list[str], metric: MetricType,
        timeframe: TimeframeType, from_date: date, to_date: date,
    ) -> list[CostNamedItem]:
        return await self._retrieve_cost_by_dimension(
            scope, filter_args, metric, timeframe, from_date, to_date,
            "ServiceName", name_col=2, currency_col=3,
        )

    async def retrieve_cost_by_location(
        self, scope: Scope, filter_args: list[str], metric: MetricType,
        timeframe: TimeframeType, from_date: date, to_date: date,
    ) -> list[CostNamedItem]:
        return await self._retrieve_cost_by_dimension(
            scope, filter_args, metric, timeframe, from_date, to_date,
            "ResourceLocation", name_col=2, currency_col=3,
        )

    async def retrieve_cost_by_resource_group(
        self, scope: Scope, filter_args: list[str], metric: MetricType,
        timeframe: TimeframeType, from_date: date, to_date: date,
    ) -> list[CostNamedItem]:
        # ResourceGroupName returns 5 columns (cost, costUSD, name, chargeType, currency)
        return await self._retrieve_cost_by_dimension(
            scope, filter_args, metric, timeframe, from_date, to_date,
            "ResourceGroupName", name_col=2, currency_col=4,
        )

    async def retrieve_cost_by_subscription(
        self, scope: Scope, filter_args: list[str], metric: MetricType,
        timeframe: TimeframeType, from_date: date, to_date: date,
    ) -> list[CostNamedItem]:
        return await self._retrieve_cost_by_dimension(
            scope, filter_args, metric, timeframe, from_date, to_date,
            "SubscriptionName", name_col=2, currency_col=4,
        )

    # ------------------------------------------------------------------
    # Daily costs
    # ------------------------------------------------------------------

    async def retrieve_daily_cost(
        self,
        scope: Scope,
        filter_args: list[str],
        metric: MetricType,
        dimension: str,
        timeframe: TimeframeType,
        from_date: date,
        to_date: date,
        include_tags: bool = False,
    ) -> list[CostDailyItem]:
        resolved = resolve_timeframe(timeframe, from_date, to_date, _today_utc())
        payload: dict[str, Any] = {
            "type": metric.value,
            "timeframe": resolved[0].value,
            "dataSet": {
                "granularity": "Daily",
                "aggregation": {
                    "totalCost": {"name": "Cost", "function": "Sum"},
                    "totalCostUSD": {"name": "CostUSD", "function": "Sum"},
                },
                "sorting": [{"direction": "Ascending", "name": "UsageDate"}],
                "grouping": [
                    {"type": "Dimension", "name": dimension},
                    {"type": "Dimension", "name": "ChargeType"},
                ],
                "filter": _generate_filters(filter_args),
            },
        }
        if include_tags:
            payload["dataSet"]["include"] = ["Tags"]

        tp = self._time_period(resolved)
        if tp:
            payload["timePeriod"] = tp

        path = "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000"
        rows = await self._paged_post(scope, path, payload)

        items = []
        for row in rows:
            name = str(row[3])
            d = datetime.strptime(str(row[2]), "%Y%m%d").date()
            cost = float(row[0])
            cost_usd = float(row[1])
            tags: dict[str, str] | None = None

            if include_tags:
                tags_raw = row[5] if isinstance(row[5], list) else []
                tags = {}
                for tag_str in tags_raw:
                    parts = str(tag_str).split(":", 1)
                    if len(parts) == 2:
                        tags[parts[0].strip('"')] = parts[1].strip('"')
                currency = str(row[6])
            else:
                currency = str(row[5])

            items.append(CostDailyItem(
                date=d, name=name, cost=cost, cost_usd=cost_usd,
                currency=currency, tags=tags,
            ))
        return items

    # ------------------------------------------------------------------
    # Cost by resource
    # ------------------------------------------------------------------

    async def retrieve_cost_for_resources(
        self,
        scope: Scope,
        filter_args: list[str],
        metric: MetricType,
        exclude_meter_details: bool,
        timeframe: TimeframeType,
        from_date: date,
        to_date: date,
    ) -> list[CostResourceItem]:
        if not exclude_meter_details:
            grouping = [
                {"type": "Dimension", "name": "ResourceId"},
                {"type": "Dimension", "name": "ResourceType"},
                {"type": "Dimension", "name": "ResourceLocation"},
                {"type": "Dimension", "name": "ChargeType"},
                {"type": "Dimension", "name": "ResourceGroupName"},
                {"type": "Dimension", "name": "PublisherType"},
                {"type": "Dimension", "name": "MeterCategory"},
                {"type": "Dimension", "name": "MeterSubcategory"},
                {"type": "Dimension", "name": "Meter"},
            ]
        else:
            grouping = [
                {"type": "Dimension", "name": "ResourceId"},
                {"type": "Dimension", "name": "ResourceType"},
                {"type": "Dimension", "name": "ResourceLocation"},
                {"type": "Dimension", "name": "ChargeType"},
                {"type": "Dimension", "name": "ResourceGroupName"},
                {"type": "Dimension", "name": "PublisherType"},
            ]

        resolved = resolve_timeframe(timeframe, from_date, to_date, _today_utc())
        payload = {
            "type": metric.value,
            "timeframe": resolved[0].value,
            "dataSet": {
                "granularity": "None",
                "aggregation": {
                    "totalCost": {"name": "Cost", "function": "Sum"},
                    "totalCostUSD": {"name": "CostUSD", "function": "Sum"},
                },
                "include": ["Tags"],
                "filter": _generate_filters(filter_args),
                "grouping": grouping,
            },
        }
        tp = self._time_period(resolved)
        if tp:
            payload["timePeriod"] = tp

        path = "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000"
        rows = await self._paged_post(scope, path, payload)

        items = []
        for row in rows:
            cost = float(row[0])
            cost_usd = float(row[1])
            resource_id = str(row[2])
            resource_type = str(row[3])
            resource_location = str(row[4])
            charge_type = str(row[5])
            resource_group_name = str(row[6])
            publisher_type = str(row[7])

            service_name = None if exclude_meter_details else str(row[8])
            service_tier = None if exclude_meter_details else str(row[9])
            meter = None if exclude_meter_details else str(row[10])

            tags_col = 8 if exclude_meter_details else 11
            tags_raw = row[tags_col] if isinstance(row[tags_col], list) else []
            tags: dict[str, str] = {}
            for tag_str in tags_raw:
                parts = str(tag_str).split(":", 1)
                if len(parts) == 2:
                    tags[parts[0].strip('"')] = parts[1].strip('"')

            currency_col = 9 if exclude_meter_details else 12
            currency = str(row[currency_col])

            items.append(CostResourceItem(
                cost=cost,
                cost_usd=cost_usd,
                resource_id=resource_id,
                resource_type=resource_type,
                resource_location=resource_location,
                charge_type=charge_type,
                resource_group_name=resource_group_name,
                publisher_type=publisher_type,
                service_name=service_name,
                service_tier=service_tier,
                meter=meter,
                tags=tags,
                currency=currency,
            ))

        if exclude_meter_details:
            # Aggregate multiple locations for the same resource
            from itertools import groupby as _groupby
            aggregated = []
            sorted_items = sorted(items, key=lambda x: x.resource_id)
            for resource_id, group_iter in _groupby(sorted_items, key=lambda x: x.resource_id):
                group = list(group_iter)
                first = group[0]
                aggregated.append(CostResourceItem(
                    cost=sum(i.cost for i in group),
                    cost_usd=sum(i.cost_usd for i in group),
                    resource_id=resource_id,
                    resource_type=first.resource_type,
                    resource_location=", ".join(i.resource_location for i in group),
                    charge_type=first.charge_type,
                    resource_group_name=first.resource_group_name,
                    publisher_type=first.publisher_type,
                    service_name=None,
                    service_tier=None,
                    meter=None,
                    tags=first.tags,
                    currency=first.currency,
                ))
            return aggregated
        return items

    # ------------------------------------------------------------------
    # Budgets
    # ------------------------------------------------------------------

    async def retrieve_budgets(self, scope: Scope) -> list[BudgetItem]:
        path = "/providers/Microsoft.Consumption/budgets/?api-version=2021-10-01"
        data = await self._scope_get(scope, path)
        budget_items = []
        for item in data.get("value", []):
            props = item.get("properties", {})
            budget_id = item.get("id", "")
            budget_name = item.get("name", "")
            amount = float(props.get("amount", 0))
            time_grain = props.get("timeGrain", "")
            time_period = props.get("timePeriod", {})
            start_date = datetime.fromisoformat(time_period.get("startDate", "2000-01-01"))
            end_date = datetime.fromisoformat(time_period.get("endDate", "2099-12-31"))

            current_spend = props.get("currentSpend")
            current_spend_amount = float(current_spend["amount"]) if current_spend else None
            current_spend_currency = current_spend["unit"] if current_spend else None

            forecast_spend = props.get("forecastSpend")
            forecast_amount = float(forecast_spend["amount"]) if forecast_spend else None
            forecast_currency = forecast_spend["unit"] if forecast_spend else None

            notifications: list[Notification] = []
            for notif_name, notif_val in props.get("notifications", {}).items():
                notifications.append(Notification(
                    name=notif_name,
                    enabled=notif_val.get("enabled", False),
                    operator=notif_val.get("operator", ""),
                    threshold=float(notif_val.get("threshold", 0)),
                    contact_emails=notif_val.get("contactEmails", []),
                    contact_roles=notif_val.get("contactRoles", []),
                    contact_groups=notif_val.get("contactGroups", []),
                ))

            budget_items.append(BudgetItem(
                name=budget_name,
                id=budget_id,
                amount=amount,
                time_grain=time_grain,
                start_date=start_date,
                end_date=end_date,
                current_spend_amount=current_spend_amount,
                current_spend_currency=current_spend_currency,
                forecast_amount=forecast_amount,
                forecast_currency=forecast_currency,
                notifications=notifications,
            ))
        return budget_items

    # ------------------------------------------------------------------
    # Usage details (for what-if)
    # ------------------------------------------------------------------

    async def retrieve_usage_details(
        self, scope: Scope, filter_str: str, from_date: date, to_date: date
    ) -> list[UsageDetails]:
        date_filter = (
            f"properties/usageStart ge '{from_date.strftime('%Y-%m-%d')}' "
            f"and properties/usageEnd le '{to_date.strftime('%Y-%m-%d')}'"
        )
        combined_filter = f"{filter_str} AND {date_filter}" if filter_str else date_filter
        path = (
            "/providers/Microsoft.Consumption/usageDetails"
            "?api-version=2023-05-01&$expand=meterDetails&metric=usage&$top=5000"
            f"&$filter={combined_filter}"
        )

        items = []
        url: str | None = self._build_url(scope, path)
        client = await self._ensure_client()
        token = await self._get_token()
        headers = {"Authorization": f"Bearer {token}"}

        while url:
            response = await client.get(url, headers=headers)
            response.raise_for_status()
            data = response.json()
            for raw in data.get("value", []):
                raw_props = raw.get("properties", {})
                meter_details_raw = raw_props.get("meterDetails") or {}
                meter_details = MeterDetails(
                    meter_category=meter_details_raw.get("meterCategory", ""),
                    unit_of_measure=meter_details_raw.get("unitOfMeasure", ""),
                    meter_name=meter_details_raw.get("meterName", ""),
                    meter_sub_category=meter_details_raw.get("meterSubCategory", ""),
                )
                props = UsageProperties(
                    billing_period_start_date=raw_props.get("billingPeriodStartDate", ""),
                    billing_period_end_date=raw_props.get("billingPeriodEndDate", ""),
                    billing_profile_id=raw_props.get("billingProfileId", ""),
                    billing_profile_name=raw_props.get("billingProfileName", ""),
                    subscription_id=raw_props.get("subscriptionId", ""),
                    subscription_name=raw_props.get("subscriptionName", ""),
                    date=raw_props.get("date", ""),
                    product=raw_props.get("product", ""),
                    meter_id=raw_props.get("meterId", ""),
                    quantity=float(raw_props.get("quantity", 0)),
                    effective_price=float(raw_props.get("effectivePrice", 0)),
                    cost=float(raw_props.get("cost", 0)),
                    unit_price=float(raw_props.get("unitPrice", 0)),
                    billing_currency=raw_props.get("billingCurrency", ""),
                    resource_location=raw_props.get("resourceLocation", ""),
                    consumed_service=raw_props.get("consumedService", ""),
                    resource_id=raw_props.get("resourceId", ""),
                    resource_name=raw_props.get("resourceName", ""),
                    additional_info=raw_props.get("additionalInfo", ""),
                    resource_group=raw_props.get("resourceGroup", ""),
                    meter_details=meter_details,
                    charge_type=raw_props.get("chargeType", ""),
                    frequency=raw_props.get("frequency", ""),
                    publisher_type=raw_props.get("publisherType", ""),
                    is_azure_credit_eligible=raw_props.get("isAzureCreditEligible", False),
                    offer_id=raw_props.get("offerId", ""),
                )
                items.append(UsageDetails(
                    kind=raw.get("kind", ""),
                    id=raw.get("id", ""),
                    name=raw.get("name", ""),
                    type=raw.get("type", ""),
                    tags=raw.get("tags") or {},
                    properties=props,
                ))

            next_link = data.get("nextLink")
            url = next_link if next_link else None
        return items

    async def close(self):
        if self._client and not self._client.is_closed:
            await self._client.aclose()
