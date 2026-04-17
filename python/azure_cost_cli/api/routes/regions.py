"""GET /regions – mirrors the regions CLI command."""
from __future__ import annotations

from fastapi import APIRouter, HTTPException

from azure_cost_cli.api.schemas import RegionResponse
from azure_cost_cli.regions_api import AzureRegionsRetriever

router = APIRouter(prefix="/regions", tags=["Regions"])


@router.get("", response_model=list[RegionResponse], summary="List all available Azure regions")
async def get_regions():
    retriever = AzureRegionsRetriever()
    try:
        region_list = await retriever.retrieve_regions()
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc

    return [
        RegionResponse(
            id=r.id,
            displayName=r.display_name,
            continent=r.continent,
            location=r.location,
            latitude=r.latitude,
            longitude=r.longitude,
            isOpen=r.is_open,
        )
        for r in region_list
    ]
