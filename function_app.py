"""Azure Functions entrypoint for deployments from repository root."""
from __future__ import annotations

import sys
from pathlib import Path

import azure.functions as func

sys.path.insert(0, str(Path(__file__).resolve().parent / "python"))
from azure_cost_cli.api.app import app as flask_app  # noqa: E402
from azure_cost_cli.function_routes import create_flask_proxy, register_routes  # noqa: E402

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)
flask_proxy = create_flask_proxy(flask_app)

register_routes(app, flask_proxy)
