azd up -e "agents-e2e-local" -l "East US 2" --no-prompt

uv sync
uv run pytest -vs

azd down --force --purge