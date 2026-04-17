from __future__ import annotations

from unittest.mock import MagicMock

import function_app


def test_flask_proxy_uses_wsgi_middleware(monkeypatch):
    req = MagicMock()
    context = MagicMock()
    expected_response = MagicMock()

    middleware_instance = MagicMock()
    middleware_instance.handle.return_value = expected_response
    middleware_cls = MagicMock(return_value=middleware_instance)

    monkeypatch.setattr(function_app.func, "WsgiMiddleware", middleware_cls)

    response = function_app.flask_proxy(req, context)

    assert response is expected_response
    middleware_cls.assert_called_once_with(function_app.flask_app.wsgi_app)
    middleware_instance.handle.assert_called_once_with(req, context)
