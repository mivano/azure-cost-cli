"""Azure Functions entrypoint for hosting the Flask API."""
from __future__ import annotations

import azure.functions as func

from azure_cost_cli.api.app import app as flask_app
from azure_cost_cli.function_routes import create_flask_proxy, register_routes

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)
flask_proxy = create_flask_proxy(flask_app)

register_routes(app, flask_proxy)
