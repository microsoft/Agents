"""
A2A Pattern Test Runner — Interactive CLI

Runs A2A communication patterns and makes each pattern FEEL distinct:
  - Ping:   Wait for full orchestrator-synthesized response (like an API call)
  - Stream: See raw ADK analysis text appear live as SSE chunks arrive
  - Push:   Returns immediately, runs in background, type "check" to see results

Usage:
  python test_demo.py              Interactive mode — type your own queries
  python test_demo.py auto         Run all hardcoded tests automatically
  python test_demo.py ping         Run only the ping test
  python test_demo.py stream       Run only the stream test
  python test_demo.py compare      Run the comparison test
  python test_demo.py glossary     Run the glossary test
  python test_demo.py discover     Run agent discovery test
"""

import asyncio
import json
import logging
import sys
import time

# Suppress all internal logs so only test output is visible
logging.disable(logging.CRITICAL)

import httpx
from dotenv import load_dotenv

load_dotenv()

from brand_intelligence_advisor.orchestrator import AgentOrchestrator
from brand_intelligence_advisor.tools.a2a_client import A2AClient
from brand_intelligence_advisor.tools.brand_advisor import BrandAdvisor


# ── Config ────────────────────────────────────────────────────────────────────

ADK_AGENT_URL = "http://localhost:8080"
WEBHOOK_URL = "http://localhost:3978/a2a/webhook"


# ── Display Helpers ───────────────────────────────────────────────────────────

def header(title: str):
    print()
    print("=" * 60)
    print(f"  {title}")
    print("=" * 60)


def section(title: str):
    print()
    print(f"--- {title} ---")
    print()


def result(label: str, passed: bool, detail: str = ""):
    status = "PASS" if passed else "FAIL"
    mark = "[+]" if passed else "[X]"
    line = f"  {mark} {label}: {status}"
    if detail:
        line += f"  ({detail})"
    print(line)


def show_response(text: str, max_lines: int = 15):
    """Show a truncated response for demo readability."""
    lines = text.strip().split("\n")
    for line in lines[:max_lines]:
        print(f"    {line}")
    if len(lines) > max_lines:
        print(f"    ... ({len(lines) - max_lines} more lines)")


# ── Test Cases ────────────────────────────────────────────────────────────────

async def test_discover():
    """Test A2A Agent Discovery (/.well-known/agent-card.json)."""
    section("Test: Agent Discovery (A2A Protocol)")

    print("  Discovering remote agent at:", ADK_AGENT_URL)
    start = time.time()

    async with httpx.AsyncClient() as client:
        r = await client.get(f"{ADK_AGENT_URL}/.well-known/agent-card.json")

    elapsed = time.time() - start
    passed = r.status_code == 200

    if passed:
        card = r.json()
        print(f"  Agent Name  : {card.get('name', 'N/A')}")
        print(f"  Description : {card.get('description', 'N/A')[:80]}")
        print(f"  URL         : {card.get('url', 'N/A')}")

        caps = card.get("capabilities", {})
        print(f"  Streaming   : {caps.get('streaming', False)}")
        print(f"  Push Notify : {caps.get('pushNotifications', False)}")

    result("Agent Discovery", passed, f"{elapsed:.1f}s")
    return passed


async def test_ping(orch: AgentOrchestrator):
    """Test A2A Ping (message/send) via LLM orchestration."""
    section("Test: Ping - Quick Analysis (message/send)")

    query = "How is Nike performing in search optimization?"
    print(f'  Query: "{query}"')
    print()

    start = time.time()
    response = await orch.process_message(query, "test-ping")
    elapsed = time.time() - start

    passed = len(response) > 20
    print("  Response:")
    show_response(response)

    result("Ping (message/send)", passed, f"{elapsed:.1f}s")
    return passed


async def test_stream(orch: AgentOrchestrator):
    """Test A2A Stream (message/stream SSE) via LLM orchestration."""
    section("Test: Stream - Detailed Report (message/stream SSE)")

    query = "Give me a detailed report on Adidas shoes performance"
    print(f'  Query: "{query}"')
    print()

    start = time.time()
    response = await orch.process_message(query, "test-stream")
    elapsed = time.time() - start

    passed = len(response) > 50
    print("  Response:")
    show_response(response)

    result("Stream (message/stream)", passed, f"{elapsed:.1f}s")
    return passed


async def test_compare(orch: AgentOrchestrator):
    """Test multi-tool comparison (LLM calls analyze_brand twice)."""
    section("Test: Comparison - Multi-Tool (Nike vs Adidas)")

    query = "Compare Nike vs Adidas in sportswear"
    print(f'  Query: "{query}"')
    print()

    start = time.time()
    response = await orch.process_message(query, "test-compare")
    elapsed = time.time() - start

    passed = len(response) > 50
    print("  Response:")
    show_response(response)

    result("Comparison (multi-tool)", passed, f"{elapsed:.1f}s")
    return passed


async def test_glossary(orch: AgentOrchestrator):
    """Test local tool (SEO glossary, no A2A call)."""
    section("Test: SEO Glossary - Local Tool (no A2A call)")

    query = "What is brand visibility?"
    print(f'  Query: "{query}"')
    print()

    start = time.time()
    response = await orch.process_message(query, "test-glossary")
    elapsed = time.time() - start

    passed = len(response) > 20
    print("  Response:")
    show_response(response)

    result("Glossary (local tool)", passed, f"{elapsed:.1f}s")
    return passed


# ── Runner ────────────────────────────────────────────────────────────────────

TESTS = {
    "discover": test_discover,
    "ping": test_ping,
    "stream": test_stream,
    "compare": test_compare,
    "glossary": test_glossary,
}


# ── Transmission Mode Helpers ─────────────────────────────────────────────────

MODE_LABELS = {"1": "ping", "2": "stream", "3": "push", "4": "auto"}


def prompt_mode() -> str:
    """Show mode menu and return mode label."""
    print()
    print("  Choose A2A transmission pattern:")
    print("    [1] Ping   - synchronous request/response (wait for full result)")
    print("    [2] Stream - SSE live typing (see text arrive chunk by chunk)")
    print("    [3] Push   - fire & forget (returns immediately, check later)")
    print("    [4] Auto   - let the LLM decide (default)")
    try:
        choice = input("  Mode [1/2/3/4, default=4]: ").strip()
    except (EOFError, KeyboardInterrupt):
        return "auto"
    if choice not in MODE_LABELS:
        choice = "4"
    return MODE_LABELS[choice]


# ── Interactive Mode ──────────────────────────────────────────────────────────

async def interactive_mode():
    """Interactive mode — each A2A pattern feels genuinely different."""
    header("A2A Interactive Test Runner")
    print("  Orchestrator : Semantic Kernel + Azure OpenAI")
    print("  Remote Agent : Google ADK (A2A Protocol v0.3)")
    print("  Framework    : Microsoft 365 Agents SDK")
    print()
    print("  Each pattern delivers a DIFFERENT experience:")
    print("    Ping   -> Wait, then get full orchestrator strategic synthesis")
    print("    Stream -> See raw ADK analysis appear live (SSE chunks)")
    print("    Push   -> Returns instantly, type 'check' for results later")
    print()
    print("  Examples:")
    print('    > How is Nike doing in Active category?')
    print('    > Analyze Adidas in sportswear')
    print('    > What is brand visibility?')
    print()
    print("  Commands: 'check' (push results), 'discover', 'quit'")
    print("=" * 60)

    # Check ADK agent
    try:
        async with httpx.AsyncClient(timeout=5) as c:
            r = await c.get(f"{ADK_AGENT_URL}/.well-known/agent-card.json")
            assert r.status_code == 200
        print("  [OK] ADK agent is running on port 8080")
    except Exception:
        print("  [!!] ADK agent is NOT running on port 8080")
        print("  Start it first: poetry run python run_a2a.py")
        sys.exit(1)

    # Init components
    a2a_client = A2AClient(ADK_AGENT_URL)
    advisor = BrandAdvisor()
    push_jobs: dict[str, dict] = {}  # task_id → {query, status, result, start_time}

    try:
        orch = AgentOrchestrator(
            a2a_client=a2a_client,
            advisor=advisor,
            push_notifications=[],
            webhook_url=WEBHOOK_URL,
        )
        print("  [OK] LLM orchestrator initialized")
    except Exception as e:
        print(f"  [!!] Could not initialize orchestrator: {e}")
        sys.exit(1)

    print()

    # ── Background push completion checker ──
    async def _run_push_job(query_text: str, a2a_request: str, job_id: str):
        """Run the A2A call in background and store result when done."""
        try:
            task = await a2a_client.send_message(a2a_request)
            push_jobs[job_id]["status"] = "completed"
            push_jobs[job_id]["result"] = task.message or f"Task {task.status}"
            push_jobs[job_id]["elapsed"] = time.time() - push_jobs[job_id]["start_time"]
            # Notify user if they're at the prompt
            print(f"\n  ** [PUSH] Job '{query_text[:40]}' completed! Type 'check' to see results. **")
            print("You> ", end="", flush=True)
        except Exception as e:
            push_jobs[job_id]["status"] = "failed"
            push_jobs[job_id]["result"] = f"Error: {e}"

    # Use run_in_executor so input() doesn't block the event loop
    # (needed for background push tasks to actually run)
    loop = asyncio.get_event_loop()

    async def async_input(prompt: str) -> str:
        return await loop.run_in_executor(None, input, prompt)

    while True:
        try:
            query = await async_input("You> ")
        except (EOFError, KeyboardInterrupt):
            print("\n\nGoodbye!")
            break

        query = query.strip()
        if not query:
            continue
        if query.lower() in ("quit", "exit", "q"):
            print("\nGoodbye!")
            break

        # ── Special commands ──
        if query.lower() == "discover":
            await test_discover()
            continue

        if query.lower() == "check":
            # Show push job results
            if not push_jobs:
                print("  No push jobs submitted yet.\n")
                continue
            print()
            for job_id, job in push_jobs.items():
                status_icon = {"completed": "+", "working": "~", "failed": "X"}.get(job["status"], "?")
                print(f"  [{status_icon}] {job['query'][:50]}")
                print(f"      Status: {job['status']}")
                if job["status"] == "completed":
                    elapsed = job.get("elapsed", 0)
                    print(f"      Completed in: {elapsed:.1f}s")
                    print(f"      Result ({len(job['result'])} chars):")
                    for line in job["result"].strip().split("\n")[:20]:
                        print(f"        {line}")
                    remaining = len(job["result"].strip().split("\n")) - 20
                    if remaining > 0:
                        print(f"        ... ({remaining} more lines)")
                elif job["status"] == "working":
                    elapsed = time.time() - job["start_time"]
                    print(f"      Running for: {elapsed:.0f}s")
                print()
            continue

        # ── Mode selection ──
        # (quick prompt — ok to block briefly)
        mode = prompt_mode()

        # ── Parse query for stream/push (need brand + category) ──
        parsed = advisor.parse_query(query)
        a2a_request = advisor.formulate_a2a_request(parsed) if parsed.is_valid else None

        # ================================================================
        # PING — Full SK orchestration (understand -> A2A -> synthesize)
        # ================================================================
        if mode in ("ping", "auto"):
            hint = "You MUST use mode='ping' (synchronous message/send) for this request." if mode == "ping" else ""
            message = f"{query}\n\n[INSTRUCTION: {hint}]" if hint else query

            print()
            print(f"  [PING] Sending to SK orchestrator...")
            print(f"  [PING] Waiting for complete response...")
            print()
            start = time.time()
            try:
                response = await orch.process_message(message, "interactive")
                elapsed = time.time() - start
                print(f"Advisor ({elapsed:.1f}s) [pattern: ping]:")
                print()
                for line in response.strip().split("\n"):
                    print(f"  {line}")
                print()
            except Exception as e:
                print(f"  Error: {e}\n")

        # ================================================================
        # STREAM — Raw SSE chunks printed live as they arrive
        # ================================================================
        elif mode == "stream":
            if not a2a_request:
                # If we can't parse brand/category, fall back to orchestrator
                print(f"  [STREAM] Could not parse brand from query, using orchestrator...")
                response = await orch.process_message(query, "interactive")
                print(f"\n  {response}\n")
                continue

            print()
            print(f"  [STREAM] Opening SSE connection to ADK agent...")
            print(f"  [STREAM] Request: {a2a_request}")
            print(f"  [STREAM] Chunks will appear as they arrive from the server:")
            print()
            print("-" * 60)

            start = time.time()
            chunk_count = 0
            try:
                async for event in a2a_client.stream_message(a2a_request):
                    chunk_count += 1
                    if event.text:
                        # Print each chunk immediately as it arrives — live typing!
                        sys.stdout.write(event.text)
                        sys.stdout.flush()
                    elif event.event_type:
                        # Show status events
                        status = event.data.get("result", {}).get("status", {}).get("state", "")
                        if status:
                            sys.stdout.write(f"\n  [SSE event: {status}]")
                            sys.stdout.flush()

                elapsed = time.time() - start
                print()
                print("-" * 60)
                print(f"  [STREAM] Done — {chunk_count} SSE events in {elapsed:.1f}s")
                print()
            except Exception as e:
                print(f"\n  Stream error: {e}\n")

        # ================================================================
        # PUSH — Fire & forget, runs in background, check later
        # ================================================================
        elif mode == "push":
            if not a2a_request:
                print(f"  [PUSH] Could not parse brand from query, using orchestrator...")
                response = await orch.process_message(query, "interactive")
                print(f"\n  {response}\n")
                continue

            job_id = f"push-{len(push_jobs) + 1}"
            push_jobs[job_id] = {
                "query": query,
                "status": "working",
                "result": None,
                "start_time": time.time(),
            }

            # Fire off the A2A call in the background — don't wait!
            asyncio.create_task(_run_push_job(query, a2a_request, job_id))

            print()
            print(f"  [PUSH] Job submitted! ID: {job_id}")
            print(f"  [PUSH] Request: {a2a_request}")
            print(f"  [PUSH] Running in background — you can keep typing!")
            print(f"  [PUSH] Type 'check' when you want to see results.")
            print()
            print(f"  Try asking something else while you wait:")
            print(f"    > What is brand visibility?")
            print(f"    > How is Puma doing in Active category?  (use ping)")
            print()


async def run_all(selected: list[str]):
    header("A2A Pattern Test Runner")
    print("  Orchestrator : Semantic Kernel + Azure OpenAI")
    print("  Remote Agent : Google ADK (A2A Protocol v0.3)")
    print("  Framework    : Microsoft 365 Agents SDK")

    # Check ADK agent is running
    try:
        async with httpx.AsyncClient(timeout=5) as c:
            r = await c.get(f"{ADK_AGENT_URL}/.well-known/agent-card.json")
            assert r.status_code == 200
    except Exception:
        print("\n  ERROR: ADK agent is not running on port 8080.")
        print("  Start it first: poetry run python run_a2a.py")
        sys.exit(1)

    # Initialize orchestrator (only needed for LLM tests)
    orch = None
    needs_orch = any(t != "discover" for t in selected)
    if needs_orch:
        try:
            orch = AgentOrchestrator(
                a2a_client=A2AClient(ADK_AGENT_URL),
                advisor=BrandAdvisor(),
                push_notifications=[],
                webhook_url=WEBHOOK_URL,
            )
        except Exception as e:
            print(f"\n  ERROR: Could not initialize orchestrator: {e}")
            sys.exit(1)

    # Run tests
    results = {}
    total_start = time.time()

    for name in selected:
        test_fn = TESTS[name]
        try:
            if name == "discover":
                passed = await test_fn()
            else:
                passed = await test_fn(orch)
            results[name] = passed
        except Exception as e:
            print(f"\n  ERROR in {name}: {e}")
            results[name] = False

    total_elapsed = time.time() - total_start

    # Summary
    header("Test Summary")
    passed_count = sum(1 for v in results.values() if v)
    total_count = len(results)

    for name, passed in results.items():
        result(name.capitalize(), passed)

    print()
    print(f"  {passed_count}/{total_count} passed in {total_elapsed:.1f}s")
    print()

    return all(results.values())


def main():
    args = sys.argv[1:]

    if args and args[0] in ("--help", "-h"):
        print(__doc__)
        sys.exit(0)

    # Default: interactive mode (no args)
    if not args:
        asyncio.run(interactive_mode())
        return

    # "auto" runs all hardcoded tests
    if args == ["auto"]:
        selected = list(TESTS.keys())
        success = asyncio.run(run_all(selected))
        sys.exit(0 if success else 1)

    # Specific test names
    selected = []
    for a in args:
        if a.lower() in TESTS:
            selected.append(a.lower())
        else:
            print(f"Unknown test: {a}")
            print(f"Available: {', '.join(TESTS.keys())}, auto")
            sys.exit(1)

    success = asyncio.run(run_all(selected))
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
