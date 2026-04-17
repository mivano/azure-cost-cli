# azure-cost-cli

Python implementation of Azure Cost CLI with a Flask REST API.

## Quick start

```bash
pip install ./python
azure-cost --help
```

## Flask API

```bash
azure-cost-api --host 0.0.0.0 --port 8000
```

Available endpoints include:
- `/accumulated-cost`
- `/daily-costs`
- `/cost-by-resource`
- `/cost-by-tag`
- `/budgets`
- `/detect-anomalies`
- `/diff`
- `/regions`
- `/what-if/region`
- `/what-if/devtest`
- `/health`

For full usage and development details, see `/python/README.md`.

## Azure Functions deployment

Azure Functions requires `host.json` at the app package root during deployment.
This repository includes root-level `host.json` and `function_app.py` so deployments from repository root work without path remapping.
