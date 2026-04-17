"""Flask application assembly.

Start the server:
    azure-cost-api                              # default host/port
    azure-cost-api --host 0.0.0.0 --port 8080
"""
from __future__ import annotations

import argparse
import asyncio
import inspect
from datetime import date
from typing import Any, get_args, get_origin, get_type_hints

from flask import Flask, jsonify, request
from pydantic import BaseModel

from azure_cost_cli.api._compat import HTTPException, QueryParam
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

app = Flask(__name__)
app.config["JSON_SORT_KEYS"] = False
app.config["API_VERSION"] = "1.0.0"


def _parse_bool(value: str) -> bool:
    return value.strip().lower() in {"1", "true", "t", "yes", "y", "on"}


def _unwrap_type(annotation: Any) -> tuple[Any, QueryParam | None]:
    origin = get_origin(annotation)
    if origin is None:
        return annotation, None

    if str(origin) == "<class 'typing.Annotated'>":
        args = get_args(annotation)
        base = args[0]
        query_meta = next((m for m in args[1:] if isinstance(m, QueryParam)), None)
        return base, query_meta

    return annotation, None


def _parse_single(value: str, target_type: Any) -> Any:
    origin = get_origin(target_type)
    if origin is not None:
        args = [a for a in get_args(target_type) if a is not type(None)]
        if len(args) == 1:
            target_type = args[0]
            origin = get_origin(target_type)

    if target_type is str or target_type is Any:
        return value
    if target_type is bool:
        return _parse_bool(value)
    if target_type is int:
        return int(value)
    if target_type is float:
        return float(value)
    if target_type is date:
        return date.fromisoformat(value)
    return value


def _bind_arguments(func):
    signature = inspect.signature(func)
    hints = get_type_hints(func, include_extras=True)
    kwargs: dict[str, Any] = {}

    for name, param in signature.parameters.items():
        hinted = hints.get(name, Any)
        target_type, query_meta = _unwrap_type(hinted)

        alias = query_meta.alias if query_meta and query_meta.alias else None
        key = alias if alias and alias in request.args else name

        is_list = get_origin(target_type) in (list,)

        if is_list:
            list_type_args = get_args(target_type)
            item_type = list_type_args[0] if list_type_args else str
            raw_values = request.args.getlist(key)
            if not raw_values and key != name:
                raw_values = request.args.getlist(name)
            if raw_values:
                kwargs[name] = [_parse_single(v, item_type) for v in raw_values]
            else:
                kwargs[name] = list(param.default) if isinstance(param.default, list) else []
            continue

        if key in request.args:
            raw_value = request.args.get(key)
            try:
                kwargs[name] = _parse_single(raw_value, target_type)
            except Exception as exc:  # noqa: BLE001
                raise HTTPException(status_code=400, detail=f"Invalid value for '{key}': {raw_value}") from exc
            continue

        if name in request.args:
            raw_value = request.args.get(name)
            try:
                kwargs[name] = _parse_single(raw_value, target_type)
            except Exception as exc:  # noqa: BLE001
                raise HTTPException(status_code=400, detail=f"Invalid value for '{name}': {raw_value}") from exc
            continue

        if param.default is inspect.Signature.empty or param.default is Ellipsis:
            missing = alias or name
            raise HTTPException(status_code=400, detail=f"Missing required query parameter '{missing}'")

        kwargs[name] = param.default

    return kwargs


def _to_jsonable(value: Any) -> Any:
    if isinstance(value, BaseModel):
        return value.model_dump(by_alias=True)
    if isinstance(value, list):
        return [_to_jsonable(v) for v in value]
    if isinstance(value, tuple):
        return [_to_jsonable(v) for v in value]
    if isinstance(value, dict):
        return {k: _to_jsonable(v) for k, v in value.items()}
    return value


def _invoke_handler(handler):
    kwargs = _bind_arguments(handler)
    if inspect.iscoroutinefunction(handler):
        return asyncio.run(handler(**kwargs))
    return handler(**kwargs)


def _register_router(router):
    for path, handler, method in router.routes:

        endpoint_name = f"{handler.__module__}.{handler.__name__}.{path}.{method}".replace("/", "_")

        def make_view(fn):
            def view_fn():
                try:
                    payload = _invoke_handler(fn)
                    return jsonify(_to_jsonable(payload))
                except HTTPException as exc:
                    return jsonify({"detail": exc.detail}), exc.status_code
                except Exception as exc:  # noqa: BLE001
                    return jsonify({"detail": str(exc)}), 502

            return view_fn

        app.add_url_rule(path, endpoint=endpoint_name, view_func=make_view(handler), methods=[method])


for _router in (
    accumulated_cost.router,
    daily_costs.router,
    cost_by_resource.router,
    cost_by_tag.router,
    budgets.router,
    detect_anomalies.router,
    diff.router,
    regions.router,
    whatif.router,
):
    _register_router(_router)


@app.get("/health")
def health():
    return jsonify({"status": "ok", "version": app.config["API_VERSION"]})


@app.get("/")
def root():
    return jsonify({"message": "Azure Cost CLI API", "docs": "/docs", "openapi": "/openapi.json"})


@app.get("/openapi.json")
def openapi_json():
    paths = {rule.rule: {} for rule in app.url_map.iter_rules() if rule.rule not in {"/static/<path:filename>"}}
    return jsonify({"openapi": "3.1.0", "info": {"title": "Azure Cost CLI API", "version": app.config["API_VERSION"]}, "paths": paths})


@app.get("/docs")
def docs():
    return "<html><body><h1>Azure Cost CLI API</h1><p>See /openapi.json for endpoint listing.</p></body></html>", 200



def run_server():
    parser = argparse.ArgumentParser(description="Azure Cost CLI – Flask API Server")
    parser.add_argument("--host", default="127.0.0.1", help="Bind host (default: 127.0.0.1)")
    parser.add_argument("--port", default=8000, type=int, help="Bind port (default: 8000)")
    parser.add_argument("--reload", action="store_true", help="Enable debug reloader")
    args = parser.parse_args()

    app.run(host=args.host, port=args.port, debug=args.reload)


if __name__ == "__main__":
    run_server()
