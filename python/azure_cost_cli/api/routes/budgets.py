"""GET /budgets – mirrors the budgets CLI command."""
from __future__ import annotations

from typing import Annotated, Optional

from fastapi import APIRouter, HTTPException, Query

from azure_cost_cli.api.dependencies import (
    get_scope,
    get_resolved_subscription,
    make_retriever,
)
from azure_cost_cli.api.schemas import BudgetNotification, BudgetResponse

router = APIRouter(prefix="/budgets", tags=["Budgets"])


@router.get("", response_model=list[BudgetResponse], summary="List configured Azure budgets")
async def get_budgets(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    cost_api_base_address: Annotated[str, Query(alias="costApiBaseAddress")] = "https://management.azure.com/",
    http_timeout: Annotated[int, Query(alias="httpTimeout")] = 100,
    debug: bool = False,
):
    scope = get_scope(subscription, resource_group, billing_account, enrollment_account)
    resolved_sub = get_resolved_subscription(subscription, resource_group, billing_account, enrollment_account)
    scope = get_scope(resolved_sub, resource_group, billing_account, enrollment_account)

    retriever = make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        budget_items = await retriever.retrieve_budgets(scope)
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    result: list[BudgetResponse] = []
    for b in budget_items:
        notifications = [
            BudgetNotification(
                name=n.name,
                enabled=n.enabled,
                operator=n.operator,
                threshold=n.threshold,
                contactEmails=n.contact_emails,
            )
            for n in (b.notifications or [])
        ]

        result.append(
            BudgetResponse(
                name=b.name,
                id=b.id,
                amount=b.amount,
                timeGrain=b.time_grain,
                startDate=str(b.start_date.date()) if b.start_date else "",
                endDate=str(b.end_date.date()) if b.end_date else "",
                currentSpendAmount=b.current_spend_amount,
                currentSpendCurrency=b.current_spend_currency,
                currentSpendPercentage=(
                    (b.current_spend_amount / b.amount * 100)
                    if (b.current_spend_amount is not None and b.amount)
                    else None
                ),
                forecastAmount=b.forecast_amount,
                forecastCurrency=b.forecast_currency,
                notifications=notifications,
            )
        )

    return result
