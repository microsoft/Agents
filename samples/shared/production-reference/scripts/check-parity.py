"""Check shared control copies and local documentation links across languages."""
from pathlib import Path
import re


def main():
    repository = Path(__file__).resolve().parents[4]
    canonical = (repository / "docs/shared/readiness-matrix.md").read_text(encoding="utf-8").strip()
    for plugin in ("agents-for-js", "agents-for-python", "agents-for-net"):
        matrix = repository / f"agent-plugins/{plugin}/skills/agents-sdk-to-prod/references/readiness-matrix.md"
        if matrix.read_text(encoding="utf-8").strip() != canonical:
            raise SystemExit(f"Readiness matrix differs from shared contract: {matrix}")
    documents = [repository / "docs/README.md"]
    for folder in ("nodejs", "python", "dotnet", "shared"):
        documents.extend((repository / "docs" / folder).rglob("*.md"))
        documents.extend((repository / "samples" / folder / "production-reference").glob("*.md"))
    for plugin in ("agents-for-js", "agents-for-python", "agents-for-net"):
        documents.append(repository / f"agent-plugins/{plugin}/skills/agents-sdk-to-prod/SKILL.md")
    for document in documents:
        content = document.read_text(encoding="utf-8")
        for target in re.findall(r"\]\(([^)]+)\)", content):
            target = target.split("#")[0].strip("<>")
            if not target or "://" in target or target.startswith("mailto:"):
                continue
            if not (document.parent / target).exists():
                raise SystemExit(f"Broken local link: {document.relative_to(repository)} -> {target}")
    print("Shared matrices match; production documentation links resolve.")


if __name__ == "__main__":
    main()
