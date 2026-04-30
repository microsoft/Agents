# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""
Brand Intelligence Advisor — Domain knowledge and local capabilities.

This module provides the agent's LOCAL intelligence without needing
any LLM calls or external tool invocations:

  - Parse natural language brand queries -> extract brand, category, intent
  - Track analysis history across the conversation
  - Format raw A2A responses into clean executive summaries
  - Provide SEO / brand strategy knowledge on demand

The BrandAdvisor class is used by:
  - The SK orchestrator (via @kernel_function tools that wrap its methods)
  - The fallback regex router in agent.py (directly)
  - The test_demo.py CLI (directly)
"""

import re
import logging
from datetime import datetime, timezone
from typing import Optional
from dataclasses import dataclass, field

logger = logging.getLogger(__name__)


# ── Domain Knowledge ──────────────────────────────────────────────────────────

# Major consumer brands in sportswear, footwear, and electronics
KNOWN_BRANDS = {
    "nike", "adidas", "puma", "reebok", "new balance", "under armour",
    "asics", "skechers", "converse", "vans", "fila", "brooks",
    "hoka", "on", "salomon", "columbia", "north face", "patagonia",
    "lululemon", "gymshark", "allbirds", "crocs", "birkenstock",
    "samsung", "apple", "sony", "lg", "bose", "jbl",
}

# Categories that exist in the BigQuery public e-commerce dataset.
# When a user says a generic term (e.g., "shoes"), we map to the closest match.
PRODUCT_CATEGORIES = {
    # Exact BigQuery categories
    "active", "tops & tees", "fashion hoodies & sweatshirts",
    "jeans", "swim", "shorts", "sleep & lounge", "plus",
    "dresses", "skirts", "pants", "pants & capris",
    "suits", "suits & sport coats", "socks", "underwear",
    "accessories", "outerwear & coats", "blazers & jackets",
    "sweaters", "leggings",
    # Common user terms that map to BigQuery categories
    "shoes", "sneakers", "sportswear", "running", "training",
    "basketball", "football", "tennis",
}

# Map common user terms to actual BigQuery categories
CATEGORY_MAP = {
    "shoes": "Active",
    "sneakers": "Active",
    "sportswear": "Active",
    "running": "Active",
    "training": "Active",
    "basketball": "Active",
    "football": "Active",
    "tennis": "Active",
    "shirts": "Tops & Tees",
    "hoodies": "Fashion Hoodies & Sweatshirts",
    "jackets": "Blazers & Jackets",
    "pants": "Pants",
}

# SEO and brand optimization glossary — these definitions are surfaced
# to users when they ask "What is <term>?" or type "glossary"
SEO_GLOSSARY = {
    "brand visibility": (
        "How often and prominently a brand appears in search results. "
        "Higher visibility = more organic traffic and brand awareness."
    ),
    "keyword cannibalization": (
        "When multiple pages from the same brand compete for the same keyword, "
        "diluting ranking power. Common in large product catalogs."
    ),
    "search impression share": (
        "The percentage of total impressions a brand receives for a keyword "
        "compared to the total available impressions."
    ),
    "product title optimization": (
        "Crafting product titles with relevant keywords to improve search ranking. "
        "Key factors: brand name position, keyword density, title length."
    ),
    "competitive gap analysis": (
        "Identifying keywords where competitors rank but your brand doesn't. "
        "Reveals untapped opportunities for visibility improvement."
    ),
    "serp position": (
        "Search Engine Results Page position. Top 3 positions capture ~60% of clicks. "
        "Position 1 alone gets ~28% of all clicks."
    ),
    "generic keyword": (
        "Non-branded search terms like 'running shoes' rather than 'Nike running shoes'. "
        "Winning generic keywords drives new customer acquisition."
    ),
    "share of voice": (
        "A brand's share of total visibility across a set of target keywords. "
        "Calculated as: (brand impressions / total impressions) x 100."
    ),
    "ctr": (
        "Click-Through Rate. The percentage of people who click on a search result "
        "after seeing it. Formula: (clicks / impressions) x 100. Higher CTR indicates "
        "better title/snippet optimization."
    ),
}

# Actionable strategy tips for brand optimization
STRATEGY_TIPS = [
    "**Title-first optimization**: Place the most relevant generic keyword "
    "at the start of your product title, before the brand name.",

    "**Category coverage**: Ensure your brand has products indexed across "
    "all relevant subcategories to maximize keyword footprint.",

    "**Competitor benchmarking**: Analyze the top 3 competitors for each "
    "generic keyword to understand what title patterns rank highest.",

    "**Long-tail keywords**: Don't just target 'running shoes' -- target "
    "'cushioned running shoes for flat feet' to capture high-intent traffic.",

    "**Seasonal keyword rotation**: Update product titles quarterly to "
    "include trending seasonal terms (e.g., 'summer', 'back-to-school').",
]


# ── Parsed Query ──────────────────────────────────────────────────────────────

@dataclass
class BrandQuery:
    """Parsed user intent from a natural language message."""
    brand: Optional[str] = None
    category: Optional[str] = None
    raw_text: str = ""
    is_valid: bool = False
    error: Optional[str] = None


@dataclass
class AnalysisRecord:
    """A record of a completed brand analysis."""
    brand: str
    category: Optional[str]
    timestamp: datetime
    mode: str  # ping, sse, push
    result_summary: str
    raw_response: Optional[str] = None


# ── Brand Advisor ─────────────────────────────────────────────────────────────

class BrandAdvisor:
    """
    The agent's local intelligence layer.

    Responsible for:
      - Parsing natural language into structured brand queries
      - Tracking analysis history across the session
      - Formatting raw A2A responses into executive summaries
      - Providing SEO/strategy domain knowledge
    """

    def __init__(self):
        self._history: list[AnalysisRecord] = []

    # ── Query Parsing ─────────────────────────────────────────────────────

    def parse_query(self, text: str) -> BrandQuery:
        """
        Extract brand name and optional category from natural language.

        Examples:
          "analyze Nike socks"         -> brand=Nike, category=socks
          "check Adidas running shoes" -> brand=Adidas, category=running shoes
          "Nike"                       -> brand=Nike, category=None
          "how is Puma doing"          -> brand=Puma, category=None
        """
        text_lower = text.lower().strip()

        # Remove command prefixes
        for prefix in [
            "analyze", "check", "search", "look up", "find",
            "how is", "how are", "what about", "show me",
            "brand analysis for", "analysis of", "report on",
        ]:
            if text_lower.startswith(prefix):
                text_lower = text_lower[len(prefix):].strip()
                break

        # Remove trailing noise
        for suffix in [
            "doing", "performing", "ranking", "on google",
            "on google shopping", "please", "thanks",
        ]:
            if text_lower.endswith(suffix):
                text_lower = text_lower[: -len(suffix)].strip()
                break

        # Try to match known brands (longest first to avoid partial matches)
        brand = None
        category = None

        for known_brand in sorted(KNOWN_BRANDS, key=len, reverse=True):
            if known_brand in text_lower:
                brand = known_brand.title()
                remaining = text_lower.replace(known_brand, "").strip()

                # Remove possessive suffix (e.g., "nike's shoes" -> "shoes")
                if remaining.startswith("'s "):
                    remaining = remaining[3:].strip()
                elif remaining.startswith("'s"):
                    remaining = remaining[2:].strip()

                # Try to match category from remaining text
                if remaining:
                    for cat in sorted(PRODUCT_CATEGORIES, key=len, reverse=True):
                        if cat in remaining:
                            # Map to actual BigQuery category if needed
                            category = CATEGORY_MAP.get(cat, cat.title())
                            break
                    if not category and remaining:
                        # Use whatever's left, try mapping it
                        category = CATEGORY_MAP.get(remaining, remaining.title())

                break

        if not brand:
            # Try capitalized words as potential brand names
            words = text.strip().split()
            capitalized = [w for w in words if w[0].isupper() and len(w) > 1]
            if capitalized:
                brand = capitalized[0]
            else:
                return BrandQuery(
                    raw_text=text,
                    is_valid=False,
                    error="Could not identify a brand name. Try: 'ping Nike socks'",
                )

        return BrandQuery(
            brand=brand,
            category=category,
            raw_text=text,
            is_valid=True,
        )

    # ── A2A Request Formulation ───────────────────────────────────────────

    def formulate_a2a_request(self, query: BrandQuery) -> str:
        """
        Build a plain-text request string for the ADK agent.
        The ADK agent handles category lookup and analysis internally.
        """
        brand = query.brand
        if query.category:
            return f"Analyze {brand} in {query.category} category"
        return f"Analyze {brand}"

    # ── History Tracking ──────────────────────────────────────────────────

    def record_analysis(
        self,
        query: BrandQuery,
        mode: str,
        result_summary: str,
        raw_response: Optional[str] = None,
    ):
        """Store a completed analysis in the session history."""
        record = AnalysisRecord(
            brand=query.brand or "Unknown",
            category=query.category,
            timestamp=datetime.now(timezone.utc),
            mode=mode,
            result_summary=result_summary[:500],
            raw_response=raw_response,
        )
        self._history.append(record)
        logger.info(
            f"Recorded analysis #{len(self._history)}: {record.brand} ({mode})"
        )

    def get_history_summary(self) -> str:
        """Format analysis history for display."""
        if not self._history:
            return "No analyses performed yet in this session."

        lines = ["**Analysis History**\n"]
        for i, rec in enumerate(self._history, 1):
            cat = f" ({rec.category})" if rec.category else ""
            time_str = rec.timestamp.strftime("%H:%M:%S")
            lines.append(
                f"  {i}. **{rec.brand}**{cat} -- via `{rec.mode}` at {time_str}\n"
                f"     _{rec.result_summary[:120]}..._"
            )

        return "\n".join(lines)

    @property
    def analysis_count(self) -> int:
        return len(self._history)

    @property
    def brands_analyzed(self) -> list[str]:
        return list({r.brand for r in self._history})

    # ── Response Formatting ───────────────────────────────────────────────

    def format_executive_summary(
        self, brand: str, raw_response: str, mode: str
    ) -> str:
        """
        Wrap a raw A2A response with executive-level context.
        Adds strategic framing without hallucinating data.
        """
        header = f"**Brand Intelligence Report: {brand}**\n"
        mode_label = {"ping": "Synchronous", "sse": "Streamed", "push": "Async"}.get(
            mode, mode
        )
        meta = (
            f"_Mode: {mode_label} | Generated: "
            f"{datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}_\n"
        )
        divider = "-" * 40 + "\n"

        body = (
            raw_response if raw_response else "_No data returned from analysis agent._"
        )

        footer = (
            f"\n{divider}"
            f"**What to do next**: Use these insights to optimize product titles, "
            f"adjust bidding strategy on underperforming keywords, and monitor "
            f"competitor movements weekly.\n"
            f"\nType `history` to see past analyses or `strategy` for optimization tips."
        )

        return f"{header}{meta}{divider}{body}{footer}"

    def format_sse_chunk(self, chunk_text: str, chunk_number: int) -> str:
        """Format a single SSE chunk for display."""
        if chunk_number == 1:
            return f"**Streaming analysis...**\n\n{chunk_text}"
        return chunk_text

    def format_push_acknowledgment(self, brand: str, task_id: str) -> str:
        """Format the push notification registration acknowledgment."""
        return (
            f"**Background analysis started for {brand}**\n\n"
            f"Task ID: `{task_id[:12]}...`\n"
            f"You'll receive a notification when the analysis completes.\n"
            f"Type `status` to check for received notifications."
        )

    # ── SEO Knowledge ─────────────────────────────────────────────────────

    def get_seo_definition(self, term: str) -> Optional[str]:
        """Look up an SEO term definition (formatted for display)."""
        term_lower = term.lower().strip()
        for key, definition in SEO_GLOSSARY.items():
            if term_lower in key or key in term_lower:
                return f"**{key.title()}**\n\n{definition}"
        return None

    def get_strategy_tips(self) -> str:
        """Return brand optimization strategy tips."""
        header = "**Brand Search Optimization Strategy Tips**\n\n"
        tips = "\n\n".join(
            f"{i}. {tip}" for i, tip in enumerate(STRATEGY_TIPS, 1)
        )
        return f"{header}{tips}"

    def get_glossary(self) -> str:
        """Return the full SEO glossary."""
        header = "**SEO & Brand Optimization Glossary**\n\n"
        entries = []
        for term, definition in sorted(SEO_GLOSSARY.items()):
            entries.append(f"**{term.title()}**: {definition}")
        return header + "\n\n".join(entries)

    # ── Data Accessors (for LLM orchestrator) ─────────────────────────────

    def get_history_data(self) -> list[dict]:
        """Return raw history data as dicts (for JSON serialization by SK tools)."""
        return [
            {
                "brand": rec.brand,
                "category": rec.category,
                "mode": rec.mode,
                "timestamp": rec.timestamp.strftime("%Y-%m-%d %H:%M:%S UTC"),
                "result_summary": rec.result_summary[:300],
            }
            for rec in self._history
        ]

    def get_glossary_data(self) -> dict:
        """Return raw glossary as a dict (for JSON serialization by SK tools)."""
        return dict(SEO_GLOSSARY)

    def get_glossary_terms(self) -> list[str]:
        """Return list of available glossary term names."""
        return sorted(SEO_GLOSSARY.keys())

    def get_seo_definition_raw(self, term: str) -> Optional[str]:
        """Look up an SEO term and return the plain definition (no formatting)."""
        term_lower = term.lower().strip()
        for key, definition in SEO_GLOSSARY.items():
            if term_lower in key or key in term_lower:
                return definition
        return None

    # ── Help Text ─────────────────────────────────────────────────────────

    def get_help_text(self, agent_name: str = "Brand Search Optimization") -> str:
        """Return the help/welcome message (used in regex fallback mode)."""
        return (
            f"**Brand Intelligence Advisor**\n"
            f"_Powered by M365 Agents SDK + A2A Protocol_\n\n"
            f"I help you analyze brand visibility on Google Shopping by connecting "
            f"to the **{agent_name}** agent via A2A protocol.\n\n"
            f"**A2A Communication Modes:**\n"
            f"  - `ping <brand> [category]` -- Synchronous analysis (message/send)\n"
            f"  - `stream <brand> [category]` -- Live-streamed analysis (SSE)\n"
            f"  - `push <brand> [category]` -- Background analysis with webhook notification\n"
            f"  - `status` -- Check received push notifications\n\n"
            f"**Local Capabilities:**\n"
            f"  - `history` -- View past analyses from this session\n"
            f"  - `strategy` -- Brand optimization strategy tips\n"
            f"  - `glossary` -- SEO terminology definitions\n"
            f"  - `define <term>` -- Look up a specific SEO term\n"
            f"  - `help` -- This message\n\n"
            f"**Example:**\n"
            f"  `ping Nike socks`\n"
            f"  `stream Adidas running shoes`\n"
            f"  `push Puma sneakers`\n"
        )
