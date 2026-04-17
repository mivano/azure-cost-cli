"""Minimal compatibility helpers for route modules shared across API frameworks."""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Callable


class HTTPException(Exception):
    def __init__(self, status_code: int, detail: str):
        super().__init__(detail)
        self.status_code = status_code
        self.detail = detail


@dataclass(frozen=True)
class QueryParam:
    default: Any = None
    alias: str | None = None


def Query(default: Any = None, *, alias: str | None = None, **_: Any) -> QueryParam:
    return QueryParam(default=default, alias=alias)


class APIRouter:
    def __init__(self, *, prefix: str = "", tags: list[str] | None = None):
        self.prefix = prefix
        self.tags = tags or []
        self.routes: list[tuple[str, Callable[..., Any], str]] = []

    def get(self, path: str = "", **_: Any):
        def decorator(func: Callable[..., Any]) -> Callable[..., Any]:
            full_path = f"{self.prefix}{path}" or "/"
            if not full_path.startswith("/"):
                full_path = f"/{full_path}"
            self.routes.append((full_path, func, "GET"))
            return func

        return decorator
