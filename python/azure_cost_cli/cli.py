"""Main CLI entry point – mirrors Program.cs and all commands."""
from __future__ import annotations

import asyncio
import json
import sys
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Optional

import click

from azure_cost_cli.models import (
    MetricType,
    OutputFormat,
    Scope,
    Subscription,
    SubscriptionPolicies,
    TimeframeType,
)

# ---------------------------------------------------------------------------
# Shared option helpers
# ---------------------------------------------------------------------------

_TIMEFRAME_CHOICES = [t.value for t in TimeframeType]
_METRIC_CHOICES = [m.value for m in MetricType]
_OUTPUT_CHOICES = [o.value for o in OutputFormat]


def _get_scope(
    subscription: Optional[str],
    resource_group: Optional[str],
    billing_account: Optional[str],
    enrollment_account: Optional[str],
) -> Scope:
    if not subscription and enrollment_account and billing_account:
        return Scope.enrollment_account(billing_account, enrollment_account)
    if subscription and resource_group:
        return Scope.resource_group(subscription, resource_group)
    if billing_account:
        return Scope.billing_account(billing_account)
    return Scope.subscription(subscription or "00000000-0000-0000-0000-000000000000")


def _resolve_subscription(subscription: Optional[str], scope: Scope) -> str:
    """Return subscription id, fetching from az CLI if needed."""
    if subscription:
        return subscription
    if not scope.is_subscription_based:
        return ""
    try:
        import subprocess
        result = subprocess.run(
            ["az", "account", "show", "--query", "id", "-o", "tsv"],
            capture_output=True, text=True, check=True,
        )
        return result.stdout.strip()
    except Exception as exc:
        raise click.UsageError(
            "No subscription ID provided and unable to retrieve from Azure CLI. "
            "Please specify --subscription or run 'az login'."
        ) from exc


def _apply_auto_timeframe(
    timeframe: str, from_date: Optional[date], to_date: Optional[date]
) -> str:
    if from_date and to_date and timeframe != TimeframeType.CUSTOM.value:
        return TimeframeType.CUSTOM.value
    return timeframe


def _get_from_date(from_date: Optional[date]) -> date:
    if from_date:
        return from_date
    first_of_month = datetime.now(timezone.utc).date().replace(day=1)
    return (first_of_month - timedelta(days=1)).replace(day=1)


def _get_to_date(to_date: Optional[date]) -> date:
    return to_date or datetime.now(timezone.utc).date()


# Common shared options
def common_options(func):
    func = click.option("--subscription", "-s", default=None, help="Subscription ID")(func)
    func = click.option("--resource-group", "-g", default=None, help="Resource group")(func)
    func = click.option("--billing-account", "-b", default=None, help="Billing account ID")(func)
    func = click.option("--enrollment-account", "-e", default=None, help="Enrollment account ID")(func)
    func = click.option("--output", "-o", default="Console", type=click.Choice(_OUTPUT_CHOICES, case_sensitive=False), show_default=True, help="Output format")(func)
    func = click.option("--timeframe", "-t", default="BillingMonthToDate", type=click.Choice(_TIMEFRAME_CHOICES, case_sensitive=False), show_default=True)(func)
    func = click.option("--from", "from_date", default=None, type=click.DateTime(formats=["%Y-%m-%d"]), help="Start date (YYYY-MM-DD)")(func)
    func = click.option("--to", "to_date", default=None, type=click.DateTime(formats=["%Y-%m-%d"]), help="End date (YYYY-MM-DD)")(func)
    func = click.option("--others-cutoff", default=10, show_default=True, help="Items before collapsing into Others")(func)
    func = click.option("--query", default="", help="JMESPath query (Json output only)")(func)
    func = click.option("--use-usd/--no-use-usd", default=False, help="Force USD currency")(func)
    func = click.option("--skip-header/--no-skip-header", default=False)(func)
    func = click.option("--filter", "filter_args", multiple=True, help="Filter e.g. ResourceGroupName=rg1")(func)
    func = click.option("--metric", "-m", default="ActualCost", type=click.Choice(_METRIC_CHOICES, case_sensitive=False), show_default=True)(func)
    func = click.option("--include-tags/--no-include-tags", default=False)(func)
    func = click.option("--cost-api-base-address", default="https://management.azure.com/", show_default=True)(func)
    func = click.option("--price-api-base-address", default="https://prices.azure.com/", show_default=True)(func)
    func = click.option("--http-timeout", default=100, show_default=True)(func)
    func = click.option("--fail-if-over", default=None, type=float, help="Fail if total cost exceeds this amount")(func)
    func = click.option("--debug/--no-debug", default=False)(func)
    return func


def _make_retriever(cost_api_address, http_timeout, debug):
    from azure_cost_cli.cost_api import AzureCostApiRetriever
    return AzureCostApiRetriever(
        cost_api_address=cost_api_address,
        http_timeout=http_timeout,
        debug=debug,
    )


def _get_formatter(output: str):
    from azure_cost_cli.formatters import create_formatters
    fmt_map = create_formatters()
    out_enum = OutputFormat(output)
    return fmt_map[out_enum]


def _check_threshold(total_cost: float, fail_if_over: Optional[float], currency: str) -> int:
    if fail_if_over is not None and total_cost > fail_if_over:
        print(f"Cost threshold exceeded: {total_cost:.2f} {currency} > {fail_if_over:.2f} {currency}", file=sys.stderr)
        return 1
    return 0


# ---------------------------------------------------------------------------
# Settings object (mirrors CostSettings)
# ---------------------------------------------------------------------------

class Settings:
    def __init__(self, **kwargs):
        for k, v in kwargs.items():
            setattr(self, k, v)


# ---------------------------------------------------------------------------
# CLI group
# ---------------------------------------------------------------------------

@click.group(invoke_without_command=True)
@click.pass_context
@common_options
def main(ctx, **kwargs):
    """Azure Cost CLI – Python port of the .NET azure-cost-cli."""
    if ctx.invoked_subcommand is None:
        # Default command: accumulatedCost
        ctx.ensure_object(dict)
        ctx.invoke(accumulated_cost, **kwargs)


# ---------------------------------------------------------------------------
# accumulatedCost
# ---------------------------------------------------------------------------

@main.command("accumulatedCost")
@common_options
def accumulated_cost(**kwargs):
    """Show the accumulated cost details."""
    asyncio.run(_run_accumulated_cost(**kwargs))


async def _run_accumulated_cost(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None
    timeframe = _apply_auto_timeframe(timeframe, from_date, to_date)

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)

    try:
        if scope.is_subscription_based:
            sub = await retriever.retrieve_subscription(subscription)
        else:
            enr_disp = f" {enrollment_account}" if enrollment_account else ""
            bil_disp = f" {billing_account}" if billing_account else ""
            sub = Subscription(
                subscriptionId=scope.name,
                displayName=f"{scope.name}{enr_disp}{bil_disp}",
                state="Active",
            )

        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)
        tf = TimeframeType(timeframe)
        mt = MetricType(metric)
        filters = list(filter_args)

        costs = await retriever.retrieve_costs(scope, filters, mt, tf, from_d, to_d)
        forecasted_costs = []

        # Determine forecast window
        today = datetime.now(timezone.utc).date()
        if to_d >= today:
            fc_end = to_d.replace(day=1)
            import calendar
            last_day = calendar.monthrange(to_d.year, to_d.month)[1]
            fc_end = to_d.replace(day=last_day)
            forecasted_costs = await retriever.retrieve_forecasted_costs(
                scope, filters, mt, TimeframeType.CUSTOM, today, fc_end
            )

        by_sub = None
        if not scope.is_subscription_based:
            by_sub = await retriever.retrieve_cost_by_subscription(scope, filters, mt, tf, from_d, to_d)

        by_svc = await retriever.retrieve_cost_by_service_name(scope, filters, mt, tf, from_d, to_d)
        by_loc = await retriever.retrieve_cost_by_location(scope, filters, mt, tf, from_d, to_d)
        by_rg = await retriever.retrieve_cost_by_resource_group(scope, filters, mt, tf, from_d, to_d)

        from azure_cost_cli.models import AccumulatedCostDetails
        details = AccumulatedCostDetails(
            subscription=sub,
            enrollment_account=None,
            costs=costs,
            forecasted_costs=forecasted_costs,
            by_service_name_costs=by_svc,
            by_location_costs=by_loc,
            by_resource_group_costs=by_rg,
            by_subscription_costs=by_sub,
        )

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, others_cutoff=others_cutoff,
            subscription=subscription, _scope=scope,
        )

        formatter = _get_formatter(output)
        formatter.write_accumulated_cost(settings, details)

        total = sum((c.cost_usd if use_usd else c.cost) for c in costs)
        currency = "USD" if use_usd else (costs[0].currency if costs else "USD")
        sys.exit(_check_threshold(total, fail_if_over, currency))
    finally:
        await retriever.close()


# ---------------------------------------------------------------------------
# dailyCosts
# ---------------------------------------------------------------------------

@main.command("dailyCosts")
@common_options
@click.option("--dimension", default="ResourceGroupName", show_default=True)
def daily_costs(dimension, **kwargs):
    """Show daily cost grouped by a dimension."""
    asyncio.run(_run_daily_costs(dimension=dimension, **kwargs))


async def _run_daily_costs(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug, dimension,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None
    timeframe = _apply_auto_timeframe(timeframe, from_date, to_date)

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    # Only include tags for Json/JsonC/Csv
    if OutputFormat(output) not in (OutputFormat.JSON, OutputFormat.JSONC, OutputFormat.CSV):
        include_tags = False

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)
        tf = TimeframeType(timeframe)
        mt = MetricType(metric)

        daily = await retriever.retrieve_daily_cost(
            scope, list(filter_args), mt, dimension, tf, from_d, to_d, include_tags
        )

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, include_tags=include_tags,
            subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_daily_cost(settings, daily)

        total = sum((c.cost_usd if use_usd else c.cost) for c in daily)
        currency = "USD" if use_usd else (daily[0].currency if daily else "USD")
        sys.exit(_check_threshold(total, fail_if_over, currency))
    finally:
        await retriever.close()


# ---------------------------------------------------------------------------
# costByResource
# ---------------------------------------------------------------------------

@main.command("costByResource")
@common_options
@click.option("--exclude-meter-details/--no-exclude-meter-details", default=False)
@click.option("--top", default=0, show_default=True, help="Show only top N resources (0=all)")
@click.option("--sort", default="cost", type=click.Choice(["cost", "cost-asc", "name", "resource-group", "resource-type", "location"], case_sensitive=False), show_default=True)
def cost_by_resource(exclude_meter_details, top, sort, **kwargs):
    """Show cost details by resource."""
    asyncio.run(_run_cost_by_resource(exclude_meter_details=exclude_meter_details, top=top, sort=sort, **kwargs))


async def _run_cost_by_resource(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
    exclude_meter_details, top, sort,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None
    timeframe = _apply_auto_timeframe(timeframe, from_date, to_date)

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)
        tf = TimeframeType(timeframe)
        mt = MetricType(metric)

        resources = await retriever.retrieve_cost_for_resources(
            scope, list(filter_args), mt, exclude_meter_details, tf, from_d, to_d
        )

        # Sort
        sort_lower = sort.lower()
        if sort_lower == "cost-asc":
            resources = sorted(resources, key=lambda r: r.cost_usd if use_usd else r.cost)
        elif sort_lower == "name":
            resources = sorted(resources, key=lambda r: r.resource_id)
        elif sort_lower == "resource-group":
            resources = sorted(resources, key=lambda r: r.resource_group_name)
        elif sort_lower == "resource-type":
            resources = sorted(resources, key=lambda r: r.resource_type)
        elif sort_lower == "location":
            resources = sorted(resources, key=lambda r: r.resource_location)
        else:
            resources = sorted(resources, key=lambda r: r.cost_usd if use_usd else r.cost, reverse=True)

        total_count = len({r.resource_id for r in resources})
        total_cost = sum(r.cost_usd if use_usd else r.cost for r in resources)
        currency = resources[0].currency if resources else "USD"

        if top > 0:
            from itertools import groupby as _groupby
            sorted_by_cost = sorted(resources, key=lambda r: r.cost_usd if use_usd else r.cost, reverse=True)
            top_ids = {r.resource_id for r in sorted_by_cost[:top]}
            resources = [r for r in resources if r.resource_id in top_ids]

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_cost_by_resource(settings, resources, total_count, total_cost, currency)

        sys.exit(_check_threshold(total_cost, fail_if_over, currency))
    finally:
        await retriever.close()


# ---------------------------------------------------------------------------
# costByTag
# ---------------------------------------------------------------------------

@main.command("costByTag")
@common_options
@click.option("--tag", "tags", multiple=True, help="Tag key(s) to group by")
@click.option("--include-untagged/--no-include-untagged", default=True, show_default=True)
def cost_by_tag(tags, include_untagged, **kwargs):
    """Show cost details by tag key(s)."""
    asyncio.run(_run_cost_by_tag(tags=tags, include_untagged=include_untagged, **kwargs))


async def _run_cost_by_tag(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
    tags, include_untagged,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None
    timeframe = _apply_auto_timeframe(timeframe, from_date, to_date)

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)
        tf = TimeframeType(timeframe)
        mt = MetricType(metric)

        resources = await retriever.retrieve_cost_for_resources(
            scope, list(filter_args), mt, True, tf, from_d, to_d
        )

        by_tags = _get_resources_by_tag(resources, include_untagged, list(tags))

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_cost_by_tag(settings, by_tags)
    finally:
        await retriever.close()


def _get_resources_by_tag(resources, include_untagged: bool, tags: list[str]) -> dict:
    UNTAGGED = "(untagged)"
    result: dict[str, dict[str, list]] = {tag: {} for tag in tags}

    for resource in resources:
        res_tags = {k.lower(): v for k, v in (resource.tags or {}).items()}
        for tag in tags:
            tag_lower = tag.lower()
            if tag_lower in res_tags:
                tag_val = res_tags[tag_lower]
                result[tag].setdefault(tag_val, []).append(resource)
            elif include_untagged:
                result[tag].setdefault(UNTAGGED, []).append(resource)

    return result


# ---------------------------------------------------------------------------
# budgets
# ---------------------------------------------------------------------------

@main.command("budgets")
@common_options
def budgets(**kwargs):
    """Get the available budgets."""
    asyncio.run(_run_budgets(**kwargs))


async def _run_budgets(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
):
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        budget_items = await retriever.retrieve_budgets(scope)
        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_budgets(settings, budget_items)
    finally:
        await retriever.close()


# ---------------------------------------------------------------------------
# detectAnomalies
# ---------------------------------------------------------------------------

@main.command("detectAnomalies")
@common_options
@click.option("--dimension", default="ResourceGroupName", show_default=True)
@click.option("--recent-activity-days", default=7, show_default=True)
@click.option("--significant-change", default=0.75, show_default=True)
@click.option("--steady-growth-days", default=7, show_default=True)
@click.option("--threshold-cost", default=2.0, show_default=True)
@click.option("--exclude-removed-costs/--no-exclude-removed-costs", default=False)
def detect_anomalies(dimension, recent_activity_days, significant_change, steady_growth_days,
                     threshold_cost, exclude_removed_costs, **kwargs):
    """Detect anomalies and trends."""
    asyncio.run(_run_detect_anomalies(
        dimension=dimension,
        recent_activity_days=recent_activity_days,
        significant_change=significant_change,
        steady_growth_days=steady_growth_days,
        threshold_cost=threshold_cost,
        exclude_removed_costs=exclude_removed_costs,
        **kwargs,
    ))


async def _run_detect_anomalies(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
    dimension, recent_activity_days, significant_change, steady_growth_days,
    threshold_cost, exclude_removed_costs,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None
    timeframe = _apply_auto_timeframe(timeframe, from_date, to_date)

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)
        tf = TimeframeType(timeframe)
        mt = MetricType(metric)

        daily = await retriever.retrieve_daily_cost(
            scope, list(filter_args), mt, dimension, tf, from_d, to_d, False
        )

        from azure_cost_cli.commands.cost_analyzer import analyze_cost
        anomalies = analyze_cost(
            daily,
            recent_activity_days=recent_activity_days,
            significant_change=significant_change,
            steady_growth_days=steady_growth_days,
            threshold_cost=threshold_cost,
            exclude_removed_costs=exclude_removed_costs,
        )

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_anomaly_detection_results(settings, anomalies)
    finally:
        await retriever.close()


# ---------------------------------------------------------------------------
# diff
# ---------------------------------------------------------------------------

@main.command("diff")
@common_options
@click.option("--compare-to", default=None, help="JSON file to compare to (target)")
@click.option("--compare-from", default=None, help="JSON file to compare from (source)")
@click.option("--source-from", default=None, type=click.DateTime(formats=["%Y-%m-%d"]), help="Source period start date")
@click.option("--source-to", default=None, type=click.DateTime(formats=["%Y-%m-%d"]), help="Source period end date")
def diff(compare_to, compare_from, source_from, source_to, **kwargs):
    """Show cost difference between two timeframes."""
    asyncio.run(_run_diff(compare_to=compare_to, compare_from=compare_from,
                          source_from=source_from, source_to=source_to, **kwargs))


async def _run_diff(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
    compare_to, compare_from, source_from, source_to,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None
    source_from_d = source_from.date() if source_from else None
    source_to_d = source_to.date() if source_to else None
    timeframe = _apply_auto_timeframe(timeframe, from_date, to_date)

    has_file_params = bool(compare_to or compare_from)
    has_source_dates = bool(source_from_d and source_to_d)

    if has_file_params and has_source_dates:
        raise click.UsageError(
            "Cannot use both file-based (--compare-to/--compare-from) and "
            "live comparison (--source-from/--source-to) at the same time."
        )

    settings = Settings(
        use_usd=use_usd, output=output, query=query,
        skip_header=skip_header, subscription=subscription, others_cutoff=others_cutoff,
    )
    formatter = _get_formatter(output)

    if has_source_dates:
        scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
        subscription = _resolve_subscription(subscription, scope)
        scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
        settings._scope = scope

        retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
        try:
            if scope.is_subscription_based:
                sub = await retriever.retrieve_subscription(subscription)
            else:
                sub = Subscription(displayName=scope.name, state="Active")

            mt = MetricType(metric)
            source_details = await _fetch_accumulated_details(
                retriever, scope, list(filter_args), mt, source_from_d, source_to_d
            )
            target_from = _get_from_date(from_date)
            target_to = _get_to_date(to_date)
            target_details = await _fetch_accumulated_details(
                retriever, scope, list(filter_args), mt, target_from, target_to
            )
        finally:
            await retriever.close()
    else:
        if not (compare_to and compare_from):
            raise click.UsageError(
                "Must provide either --compare-to/--compare-from or --source-from/--source-to."
            )
        source_details = _read_accumulated_cost(compare_from)
        target_details = _read_accumulated_cost(compare_to)

    formatter.write_accumulated_diff_cost(settings, source_details, target_details)


async def _fetch_accumulated_details(retriever, scope, filters, mt, from_d, to_d):
    from azure_cost_cli.models import AccumulatedCostDetails
    tf = TimeframeType.CUSTOM
    costs = await retriever.retrieve_costs(scope, filters, mt, tf, from_d, to_d)

    today = datetime.now(timezone.utc).date()
    forecasted = []
    if to_d >= today:
        import calendar
        last_day = calendar.monthrange(to_d.year, to_d.month)[1]
        fc_end = to_d.replace(day=last_day)
        forecasted = await retriever.retrieve_forecasted_costs(scope, filters, mt, tf, today, fc_end)

    by_sub = None
    if not scope.is_subscription_based:
        by_sub = await retriever.retrieve_cost_by_subscription(scope, filters, mt, tf, from_d, to_d)

    by_svc = await retriever.retrieve_cost_by_service_name(scope, filters, mt, tf, from_d, to_d)
    by_loc = await retriever.retrieve_cost_by_location(scope, filters, mt, tf, from_d, to_d)
    by_rg = await retriever.retrieve_cost_by_resource_group(scope, filters, mt, tf, from_d, to_d)

    return AccumulatedCostDetails(
        subscription=None, enrollment_account=None,
        costs=costs, forecasted_costs=forecasted,
        by_service_name_costs=by_svc, by_location_costs=by_loc,
        by_resource_group_costs=by_rg, by_subscription_costs=by_sub,
    )


def _read_accumulated_cost(file_path: str):
    from azure_cost_cli.models import AccumulatedCostDetails, CostItem, CostNamedItem

    def _get_cost_usd(a: dict) -> float:
        """Accept both PascalCase and camelCase spellings for the USD cost field."""
        return float(a.get("CostUsd", a.get("costUsd", 0)))

    try:
        content = Path(file_path).read_text()
        data = json.loads(content)

        costs = [
            CostItem(
                date=date.fromisoformat(a["Date"]),
                cost=float(a["Cost"]),
                cost_usd=_get_cost_usd(a),
                currency=a.get("Currency", "USD"),
            )
            for a in data.get("cost", [])
        ]
        forecasted = [
            CostItem(
                date=date.fromisoformat(a["Date"]),
                cost=float(a["Cost"]),
                cost_usd=_get_cost_usd(a),
                currency=a.get("Currency", "USD"),
            )
            for a in data.get("forecastedCosts", [])
        ]
        by_svc = [
            CostNamedItem(item_name=a["ServiceName"], cost=float(a["Cost"]),
                          cost_usd=_get_cost_usd(a), currency=a.get("Currency", "USD"))
            for a in data.get("byServiceNames", [])
        ]
        by_loc = [
            CostNamedItem(item_name=a["Location"], cost=float(a["Cost"]),
                          cost_usd=_get_cost_usd(a), currency=a.get("Currency", "USD"))
            for a in data.get("ByLocation", [])
        ]
        by_rg = [
            CostNamedItem(item_name=a["ResourceGroup"], cost=float(a["Cost"]),
                          cost_usd=_get_cost_usd(a), currency=a.get("Currency", "USD"))
            for a in data.get("ByResourceGroup", [])
        ]
        return AccumulatedCostDetails(
            subscription=None, enrollment_account=None,
            costs=costs, forecasted_costs=forecasted,
            by_service_name_costs=by_svc, by_location_costs=by_loc,
            by_resource_group_costs=by_rg, by_subscription_costs=None,
        )
    except Exception as exc:
        raise click.ClickException(f"Error reading cost file {file_path}: {exc}") from exc



# ---------------------------------------------------------------------------
# regions
# ---------------------------------------------------------------------------

@main.command("regions")
@click.option("--output", "-o", default="Console", type=click.Choice(_OUTPUT_CHOICES, case_sensitive=False))
@click.option("--skip-header/--no-skip-header", default=False)
@click.option("--query", default="")
@click.option("--debug/--no-debug", default=False)
def regions(output, skip_header, query, debug):
    """Get the available Azure regions."""
    asyncio.run(_run_regions(output=output, skip_header=skip_header, query=query, debug=debug))


async def _run_regions(output, skip_header, query, debug):
    from azure_cost_cli.regions_api import AzureRegionsRetriever
    retriever = AzureRegionsRetriever()
    region_list = await retriever.retrieve_regions()

    settings = Settings(output=output, skip_header=skip_header, query=query)
    formatter = _get_formatter(output)
    formatter.write_regions(settings, region_list)


# ---------------------------------------------------------------------------
# what-if group
# ---------------------------------------------------------------------------

@main.group("what-if")
def what_if():
    """Run what-if scenarios."""


@what_if.command("region")
@common_options
def whatif_region(**kwargs):
    """Run what-if: compare costs across regions for VMs."""
    asyncio.run(_run_whatif_region(**kwargs))


async def _run_whatif_region(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    from azure_cost_cli.price_api import AzurePriceRetriever
    price_retriever = AzurePriceRetriever(price_api_address=price_api_base_address, http_timeout=http_timeout)

    try:
        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)

        usage_details = await retriever.retrieve_usage_details(scope, "", from_d, to_d)

        # Filter VMs only and group by resource_id
        from azure_cost_cli.models import UsageDetails, UsageProperties, MeterDetails
        vm_resources: dict[str, UsageDetails] = {}
        for u in usage_details:
            if (u.properties and
                    u.properties.consumed_service == "Microsoft.Compute" and
                    u.properties.meter_details and
                    u.properties.meter_details.meter_category == "Virtual Machines"):
                rid = u.properties.resource_id
                if rid not in vm_resources:
                    vm_resources[rid] = u
                else:
                    existing = vm_resources[rid]
                    existing.properties.quantity += u.properties.quantity
                    existing.properties.cost += u.properties.cost

        prices_by_region: dict = {}
        price_cache: dict[str, list] = {}

        for usage in vm_resources.values():
            sku_name = usage.properties.meter_details.meter_name if usage.properties.meter_details else ""
            meter_id = usage.properties.meter_id
            currency = usage.properties.billing_currency

            cache_key = f"{sku_name}:{meter_id}:{currency}"
            if cache_key not in price_cache:
                filter_str = f"serviceName eq 'Virtual Machines' and skuName eq '{sku_name}' and type eq 'Consumption'"
                prices = await price_retriever.get_azure_prices(currency, filter_str)
                actual = next((p for p in prices if p.meter_id == meter_id), None)
                if actual:
                    prices = [p for p in prices if p.product_name == actual.product_name]
                price_cache[cache_key] = prices
            prices_by_region[usage] = price_cache[cache_key]

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_prices_per_region(settings, prices_by_region)
    finally:
        await retriever.close()
        await price_retriever.close()


@what_if.command("devtest")
@common_options
def whatif_devtest(**kwargs):
    """Run what-if: check Dev/Test pricing for VMs."""
    asyncio.run(_run_whatif_devtest(**kwargs))


async def _run_whatif_devtest(
    subscription, resource_group, billing_account, enrollment_account,
    output, timeframe, from_date, to_date, others_cutoff, query, use_usd,
    skip_header, filter_args, metric, include_tags, cost_api_base_address,
    price_api_base_address, http_timeout, fail_if_over, debug,
):
    from_date = from_date.date() if from_date else None
    to_date = to_date.date() if to_date else None

    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    subscription = _resolve_subscription(subscription, scope)
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)

    retriever = _make_retriever(cost_api_base_address, http_timeout, debug)
    from azure_cost_cli.price_api import AzurePriceRetriever
    price_retriever = AzurePriceRetriever(price_api_address=price_api_base_address, http_timeout=http_timeout)

    try:
        from_d = _get_from_date(from_date)
        to_d = _get_to_date(to_date)

        usage_details = await retriever.retrieve_usage_details(scope, "", from_d, to_d)

        from azure_cost_cli.models import DevTestComparisonItem
        items = []
        for u in usage_details:
            if not (u.properties and
                    u.properties.consumed_service == "Microsoft.Compute" and
                    u.properties.meter_details and
                    u.properties.meter_details.meter_category == "Virtual Machines"):
                continue

            sku_name = u.properties.meter_details.meter_name
            currency = u.properties.billing_currency
            filter_str = f"serviceName eq 'Virtual Machines' and skuName eq '{sku_name}' and priceType eq 'DevTestConsumption'"
            devtest_prices = await price_retriever.get_azure_prices(currency, filter_str)
            devtest_price = next(
                (p for p in devtest_prices if p.arm_region_name == u.properties.resource_location), None
            )

            dev_unit_price = devtest_price.unit_price if devtest_price else None
            dev_cost = dev_unit_price * u.properties.quantity if dev_unit_price is not None else None
            savings = (u.properties.cost - dev_cost) if dev_cost is not None else None
            savings_pct = (savings / u.properties.cost * 100) if savings and u.properties.cost else None

            items.append(DevTestComparisonItem(
                resource_name=u.properties.resource_name,
                resource_group=u.properties.resource_group,
                product=u.properties.product,
                meter_name=sku_name,
                region=u.properties.resource_location,
                currency=currency,
                unit_of_measure=u.properties.meter_details.unit_of_measure,
                quantity=u.properties.quantity,
                current_unit_price=u.properties.unit_price,
                current_cost=u.properties.cost,
                dev_test_unit_price=dev_unit_price,
                dev_test_cost=dev_cost,
                savings=savings,
                savings_percentage=savings_pct,
            ))

        settings = Settings(
            use_usd=use_usd, output=output, query=query,
            skip_header=skip_header, subscription=subscription, _scope=scope,
        )
        formatter = _get_formatter(output)
        formatter.write_dev_test_comparison(settings, items)
    finally:
        await retriever.close()
        await price_retriever.close()


if __name__ == "__main__":
    main()
