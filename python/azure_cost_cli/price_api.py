"""Azure Retail Prices API retriever."""
from __future__ import annotations

import httpx

from azure_cost_cli.models import PriceRecord


class AzurePriceRetriever:
    def __init__(
        self,
        price_api_address: str = "https://prices.azure.com/",
        http_timeout: int = 100,
    ):
        self.price_api_address = price_api_address.rstrip("/") + "/"
        self.http_timeout = http_timeout
        self._client: httpx.AsyncClient | None = None

    async def _ensure_client(self) -> httpx.AsyncClient:
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(
                base_url=self.price_api_address,
                timeout=self.http_timeout,
                follow_redirects=True,
            )
        return self._client

    async def get_azure_prices(
        self, currency: str = "USD", filter_str: str = ""
    ) -> list[PriceRecord]:
        client = await self._ensure_client()
        url: str | None = f"api/retail/prices?currencyCode='{currency}'&$filter={filter_str}"
        items: list[PriceRecord] = []

        while url:
            response = await client.get(url)
            response.raise_for_status()
            data = response.json()
            for item in data.get("Items", []):
                items.append(PriceRecord(
                    currency_code=item.get("currencyCode", ""),
                    tier_minimum_units=float(item.get("tierMinimumUnits", 0)),
                    retail_price=float(item.get("retailPrice", 0)),
                    unit_price=float(item.get("unitPrice", 0)),
                    arm_region_name=item.get("armRegionName", ""),
                    location=item.get("location", ""),
                    effective_start_date=item.get("effectiveStartDate", ""),
                    meter_id=item.get("meterId", ""),
                    meter_name=item.get("meterName", ""),
                    product_id=item.get("productId", ""),
                    sku_id=item.get("skuId", ""),
                    product_name=item.get("productName", ""),
                    sku_name=item.get("skuName", ""),
                    service_name=item.get("serviceName", ""),
                    service_id=item.get("serviceId", ""),
                    service_family=item.get("serviceFamily", ""),
                    unit_of_measure=item.get("unitOfMeasure", ""),
                    type=item.get("type", ""),
                    is_primary_meter_region=item.get("isPrimaryMeterRegion", False),
                    arm_sku_name=item.get("armSkuName", ""),
                ))
            url = data.get("NextPageLink")
        return items

    async def close(self):
        if self._client and not self._client.is_closed:
            await self._client.aclose()
