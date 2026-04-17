"""Azure Functions entrypoint for hosting the Flask API."""
from __future__ import annotations

import azure.functions as func

from azure_cost_cli.api.app import app as flask_app

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)


@app.route(route="{*route}", methods=["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"])
def flask_proxy(req: func.HttpRequest, context: func.Context) -> func.HttpResponse:
    return func.WsgiMiddleware(flask_app.wsgi_app).handle(req, context)
