"""Abstract base class for all output formatters."""
from __future__ import annotations

from abc import ABC, abstractmethod
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from azure_cost_cli.models import (
        AccumulatedCostDetails,
        AnomalyDetectionResult,
        AzureRegion,
        BudgetItem,
        CostDailyItem,
        CostResourceItem,
        DevTestComparisonItem,
        PriceRecord,
        UsageDetails,
    )


class BaseOutputFormatter(ABC):
    @abstractmethod
    def write_accumulated_cost(self, settings: object, details: "AccumulatedCostDetails") -> None:
        ...

    @abstractmethod
    def write_cost_by_resource(
        self,
        settings: object,
        resources: list["CostResourceItem"],
        total_count: int = 0,
        total_cost: float = 0,
        currency: str = "USD",
    ) -> None:
        ...

    @abstractmethod
    def write_budgets(self, settings: object, budgets: list["BudgetItem"]) -> None:
        ...

    @abstractmethod
    def write_daily_cost(self, settings: object, daily_costs: list["CostDailyItem"]) -> None:
        ...

    @abstractmethod
    def write_anomaly_detection_results(
        self, settings: object, anomalies: list["AnomalyDetectionResult"]
    ) -> None:
        ...

    @abstractmethod
    def write_regions(self, settings: object, regions: list["AzureRegion"]) -> None:
        ...

    @abstractmethod
    def write_cost_by_tag(
        self,
        settings: object,
        by_tags: dict[str, dict[str, list["CostResourceItem"]]],
    ) -> None:
        ...

    @abstractmethod
    def write_prices_per_region(
        self,
        settings: object,
        prices_by_region: dict["UsageDetails", list["PriceRecord"]],
    ) -> None:
        ...

    @abstractmethod
    def write_dev_test_comparison(
        self, settings: object, items: list["DevTestComparisonItem"]
    ) -> None:
        ...

    @abstractmethod
    def write_accumulated_diff_cost(
        self,
        settings: object,
        source: "AccumulatedCostDetails",
        target: "AccumulatedCostDetails",
    ) -> None:
        ...
