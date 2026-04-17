"""FastAPI application assembly.

Start the server:
    azure-cost-api                              # default host/port
    azure-cost-api --host 0.0.0.0 --port 8080
    uvicorn azure_cost_cli.api.app:app --reload
"""
from __future__ import annotations

import argparse

from fastapi import FastAPI
from fastapi.responses import JSONResponse

from azure_cost_cli.api.schemas import HealthResponse
from azure_cost_cli.api.routes import (
    accumulated_cost,
    budgets,
    cost_by_resource,
    cost_by_tag,
    daily_costs,
    detect_anomalies,
    diff,
    regions,
    whatif,
)

app = FastAPI(
    title="Azure Cost CLI – REST API",
    description=(
        "REST API wrapper around every azure-cost-cli command. "
        "All endpoints accept the same parameters as the CLI flags and return structured JSON."
    ),
    version="1.0.0",
    contact={"url": "https://github.com/mivano/azure-cost-cli"},
    license_info={"name": "MIT"},
)

# Register all routers
app.include_router(accumulated_cost.router)
app.include_router(daily_costs.router)
app.include_router(cost_by_resource.router)
app.include_router(cost_by_tag.router)
app.include_router(budgets.router)
app.include_router(detect_anomalies.router)
app.include_router(diff.router)
app.include_router(regions.router)
app.include_router(whatif.router)


@app.get("/health", response_model=HealthResponse, tags=["Health"], summary="Server health check")
async def health():
    return HealthResponse(status="ok", version=app.version)


@app.get("/", include_in_schema=False)
async def root():
    return JSONResponse({"message": "Azure Cost CLI API", "docs": "/docs", "openapi": "/openapi.json"})


# ---------------------------------------------------------------------------
# Entry-point for `azure-cost-api` script
# ---------------------------------------------------------------------------

def run_server():
    """Entry-point registered in pyproject.toml as ``azure-cost-api``."""
    import uvicorn  # noqa: C0415

    parser = argparse.ArgumentParser(description="Azure Cost CLI – REST API Server")
    parser.add_argument("--host", default="127.0.0.1", help="Bind host (default: 127.0.0.1)")
    parser.add_argument("--port", default=8000, type=int, help="Bind port (default: 8000)")
    parser.add_argument("--reload", action="store_true", help="Enable auto-reload (dev only)")
    parser.add_argument("--workers", default=1, type=int, help="Number of worker processes")
    args = parser.parse_args()

    uvicorn.run(
        "azure_cost_cli.api.app:app",
        host=args.host,
        port=args.port,
        reload=args.reload,
        workers=args.workers,
    )


if __name__ == "__main__":
    run_server()
