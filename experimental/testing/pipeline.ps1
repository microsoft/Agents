param(
    [switch]${no-teardown}
)

azd up -e "rb-e2e-local" -l "East US 2" --no-prompt

uv sync
uv run pytest -vs

if (-not ${no-teardown}) {
    azd down --force --purge
}
