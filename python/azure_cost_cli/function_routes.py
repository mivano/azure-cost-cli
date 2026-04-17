"""Helpers for exposing Flask endpoints as explicit Azure Functions routes."""
from __future__ import annotations

from collections.abc import Callable, Sequence
from typing import Any

import azure.functions as func

RouteHandler = Callable[[func.HttpRequest, func.Context], func.HttpResponse]

_ROUTES: tuple[tuple[str, str, Sequence[str]], ...] = (
    ("root", "", ["GET"]),
    ("health", "health", ["GET"]),
    ("docs", "docs", ["GET"]),
    ("openapi_json", "openapi.json", ["GET"]),
    ("accumulated_cost", "accumulated-cost", ["GET"]),
    ("daily_costs", "daily-costs", ["GET"]),
    ("cost_by_resource", "cost-by-resource", ["GET"]),
    ("cost_by_tag", "cost-by-tag", ["GET"]),
    ("budgets", "budgets", ["GET"]),
    ("detect_anomalies", "detect-anomalies", ["GET"]),
    ("diff", "diff", ["GET"]),
    ("regions", "regions", ["GET"]),
    ("whatif_region", "what-if/region", ["GET"]),
    ("whatif_devtest", "what-if/devtest", ["GET"]),
)


def create_flask_proxy(flask_app: Any) -> RouteHandler:
    def flask_proxy(req: func.HttpRequest, context: func.Context) -> func.HttpResponse:
        return func.WsgiMiddleware(flask_app.wsgi_app).handle(req, context)

    return flask_proxy


def register_routes(app: func.FunctionApp, proxy: RouteHandler) -> None:
    for route_name, route, methods in _ROUTES:
        def endpoint(req: func.HttpRequest, context: func.Context) -> func.HttpResponse:
            return proxy(req, context)

        endpoint.__name__ = route_name
        app.route(route=route, methods=list(methods))(endpoint)