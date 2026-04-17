"""Azure Regions API retriever."""
from __future__ import annotations

import httpx

from azure_cost_cli.models import AzureRegion


class AzureRegionsRetriever:
    REGIONS_URL = "https://datacenters.microsoft.com/globe/data/geo/regions.json"

    def __init__(self, http_timeout: int = 100):
        self.http_timeout = http_timeout

    async def retrieve_regions(self) -> list[AzureRegion]:
        async with httpx.AsyncClient(
            timeout=self.http_timeout,
            headers={"User-Agent": "azure-cost-cli"},
            follow_redirects=True,
        ) as client:
            response = await client.get(self.REGIONS_URL)
            response.raise_for_status()
            data = response.json()

        regions = []
        for item in data:
            regions.append(AzureRegion(
                id=item.get("id", ""),
                continent=item.get("continent", ""),
                geography_id=item.get("geographyId", ""),
                display_name=item.get("displayName", ""),
                location=item.get("location", ""),
                latitude=float(item.get("latitude", 0)),
                longitude=float(item.get("longitude", 0)),
                type_id=item.get("typeId", ""),
                is_open=item.get("isOpen", False),
                year_open=item.get("yearOpen"),
                compliance_ids=item.get("complianceIds") or [],
                has_ground_station=item.get("hasGroundStation", False),
                data_residency=item.get("dataResidency", ""),
                available_to=item.get("availableTo", ""),
                availability_zones_id=item.get("availabilityZonesId", ""),
                availability_zones_nearest_region_ids=item.get("availabilityZonesNearestRegionIds") or [],
                products_by_region_link=item.get("productsByRegionLink", ""),
                products_by_region_link_non_regional=item.get("productsByRegionLinkNonRegional", ""),
                sustainability_ids=item.get("sustainabilityIds") or [],
                disaster_recovery_crossregion_ids=item.get("disasterRecoveryCrossregionIds") or [],
                disaster_recovery_inregion_ids=item.get("disasterRecoveryInregionIds") or [],
            ))
        return regions
