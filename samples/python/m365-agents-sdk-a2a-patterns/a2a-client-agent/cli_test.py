#!/usr/bin/env python3
"""
CLI Test Client for Brand Intelligence Advisor

Tests all 4 A2A communication patterns against the ADK agent directly,
and the A2A client agent's webhook endpoint for push notifications.

Usage:
    python cli_test.py                     # Interactive menu
    python cli_test.py discover            # Discover agent card
    python cli_test.py ping "Nike socks"   # Synchronous message/send
    python cli_test.py stream "Adidas"     # SSE message/stream
    python cli_test.py push "Puma shoes"   # Push notification + webhook
    python cli_test.py status              # Check webhook notifications
    python cli_test.py all "Nike socks"    # Run all patterns sequentially

Requires: httpx, httpx-sse (already in requirements.txt)
"""

import asyncio
import json
import sys
import uuid
import time
from typing import Optional

import httpx

# ── Configuration ─────────────────────────────────────────────────────────────

ADK_AGENT_URL = "http://localhost:8080"
M365_WEBHOOK_URL = "http://localhost:3978/a2a/webhook"

TIMEOUT = httpx.Timeout(connect=10.0, read=120.0, write=10.0, pool=10.0)


# ── Colors for terminal output ────────────────────────────────────────────────

class C:
    """ANSI color codes for terminal output."""
    HEADER = "\033[95m"
    BLUE = "\033[94m"
    CYAN = "\033[96m"
    GREEN = "\033[92m"
    YELLOW = "\033[93m"
    RED = "\033[91m"
    BOLD = "\033[1m"
    DIM = "\033[2m"
    END = "\033[0m"


def banner():
    print(f"""{C.CYAN}{C.BOLD}
╔══════════════════════════════════════════════════════════════╗
║           A2A CLI Test Client — Brand Intelligence          ║
║                                                              ║
║  ADK Agent:   {ADK_AGENT_URL:<44s} ║
║  M365 Webhook: {M365_WEBHOOK_URL:<43s} ║
╚══════════════════════════════════════════════════════════════╝{C.END}
""")


def section(title: str):
    print(f"\n{C.BOLD}{C.BLUE}{'─' * 60}")
    print(f"  {title}")
    print(f"{'─' * 60}{C.END}\n")


def ok(msg: str):
    print(f"  {C.GREEN}✓{C.END} {msg}")


def warn(msg: str):
    print(f"  {C.YELLOW}⚠{C.END} {msg}")


def err(msg: str):
    print(f"  {C.RED}✗{C.END} {msg}")


def info(msg: str):
    print(f"  {C.DIM}→{C.END} {msg}")


# ── A2A Protocol Helpers ──────────────────────────────────────────────────────

def make_jsonrpc(method: str, params: dict) -> dict:
    """Build a JSON-RPC 2.0 request."""
    return {
        "jsonrpc": "2.0",
        "id": str(uuid.uuid4())[:8],
        "method": method,
        "params": params,
    }


def make_message_params(text: str, context_id: Optional[str] = None) -> dict:
    """Build message/send or message/stream params."""
    return {
        "message": {
            "role": "user",
            "parts": [{"kind": "text", "text": text}],
            "messageId": str(uuid.uuid4()),
        },
        "configuration": {
            "acceptedOutputModes": ["text/plain"],
        },
        **({"contextId": context_id} if context_id else {}),
    }


# ── Pattern A: Discover ──────────────────────────────────────────────────────

async def discover(client: httpx.AsyncClient) -> dict:
    """GET /.well-known/agent-card.json — discover agent capabilities."""
    section("DISCOVER — Agent Card")

    resp = await client.get(f"{ADK_AGENT_URL}/.well-known/agent-card.json")
    resp.raise_for_status()
    card = resp.json()

    ok(f"Agent: {C.BOLD}{card['name']}{C.END}")
    ok(f"Protocol: {card.get('protocolVersion', '?')}")
    ok(f"Description: {card.get('description', '?')[:80]}")

    caps = card.get("capabilities", {})
    ok(f"Streaming:     {'✓' if caps.get('streaming') else '✗'}")
    ok(f"Push Notifs:   {'✓' if caps.get('pushNotifications') else '✗'}")
    ok(f"State History: {'✓' if caps.get('stateTransitionHistory') else '✗'}")

    skills = card.get("skills", [])
    ok(f"Skills: {len(skills)}")
    for s in skills[:5]:
        info(f"{s['name']} — {s.get('description', '')[:60]}...")

    return card


# ── Pattern B: Ping (message/send) ───────────────────────────────────────────

async def ping(client: httpx.AsyncClient, query: str) -> dict:
    """POST message/send — synchronous blocking call."""
    section(f"PING — message/send (synchronous)")
    info(f"Query: \"{query}\"")
    info("Sending JSON-RPC request... (blocking until agent responds)")

    payload = make_jsonrpc("message/send", make_message_params(query))
    start = time.time()

    resp = await client.post(f"{ADK_AGENT_URL}/", json=payload)
    elapsed = time.time() - start
    resp.raise_for_status()

    result = resp.json()

    if "error" in result:
        err(f"JSON-RPC error: {json.dumps(result['error'], indent=2)}")
        return result

    task = result.get("result", {})
    task_id = task.get("id", "unknown")
    status = task.get("status", {}).get("state", "unknown")

    ok(f"Task ID:  {task_id[:20]}...")
    ok(f"Status:   {status}")
    ok(f"Elapsed:  {elapsed:.1f}s")

    # Extract text from artifacts
    texts = []
    for artifact in task.get("artifacts", []):
        for part in artifact.get("parts", []):
            if "text" in part:
                texts.append(part["text"])

    # Also check status message
    status_msg = task.get("status", {}).get("message", {})
    if isinstance(status_msg, dict):
        for part in status_msg.get("parts", []):
            if "text" in part:
                texts.append(part["text"])

    if texts:
        full_text = "\n".join(texts)
        print(f"\n  {C.CYAN}{'─' * 50}")
        print(f"  Agent Response ({len(full_text)} chars):")
        print(f"  {'─' * 50}{C.END}")
        # Print with indentation
        for line in full_text[:2000].split("\n"):
            print(f"  {C.DIM}│{C.END} {line}")
        if len(full_text) > 2000:
            warn(f"... truncated ({len(full_text) - 2000} more chars)")
    else:
        warn("No text content in response")

    return result


# ── Pattern C: Stream (message/stream) ───────────────────────────────────────

async def stream(client: httpx.AsyncClient, query: str) -> list:
    """POST message/stream — Server-Sent Events streaming."""
    section(f"STREAM — message/stream (SSE)")
    info(f"Query: \"{query}\"")
    info("Opening SSE connection...")

    payload = make_jsonrpc("message/stream", make_message_params(query))
    events = []
    chunk_count = 0
    start = time.time()

    try:
        # Use httpx-sse for proper SSE parsing
        from httpx_sse import aconnect_sse

        async with aconnect_sse(
            client, "POST", f"{ADK_AGENT_URL}/", json=payload
        ) as event_source:
            async for sse in event_source.aiter_sse():
                chunk_count += 1
                data = json.loads(sse.data) if sse.data else {}
                events.append({"event": sse.event, "data": data})

                # Extract text if present
                result = data.get("result", {})
                status = result.get("status", {}).get("state", "")

                texts = []
                for artifact in result.get("artifacts", []):
                    for part in artifact.get("parts", []):
                        if "text" in part:
                            texts.append(part["text"])

                status_msg = result.get("status", {}).get("message", {})
                if isinstance(status_msg, dict):
                    for part in status_msg.get("parts", []):
                        if "text" in part:
                            texts.append(part["text"])

                if texts:
                    text = " ".join(texts)
                    print(
                        f"  {C.GREEN}▸{C.END} Chunk #{chunk_count} "
                        f"[{status or 'data'}]: {text[:120]}{'...' if len(text) > 120 else ''}"
                    )
                elif status:
                    print(f"  {C.YELLOW}◉{C.END} Chunk #{chunk_count} — status: {status}")

    except ImportError:
        warn("httpx-sse not installed — falling back to raw streaming")

        async with client.stream("POST", f"{ADK_AGENT_URL}/", json=payload) as resp:
            buffer = ""
            async for chunk in resp.aiter_text():
                buffer += chunk
                while "\n\n" in buffer:
                    event_text, buffer = buffer.split("\n\n", 1)
                    chunk_count += 1
                    for line in event_text.split("\n"):
                        if line.startswith("data:"):
                            try:
                                data = json.loads(line[5:].strip())
                                events.append({"event": "message", "data": data})
                                print(
                                    f"  {C.GREEN}▸{C.END} Chunk #{chunk_count}: "
                                    f"{json.dumps(data)[:120]}"
                                )
                            except json.JSONDecodeError:
                                pass

    except Exception as e:
        err(f"SSE streaming error: {e}")

    elapsed = time.time() - start
    ok(f"Stream complete: {chunk_count} events in {elapsed:.1f}s")

    return events


# ── Pattern D: Push (message/send + webhook) ─────────────────────────────────

async def push(client: httpx.AsyncClient, query: str) -> dict:
    """
    Push notification pattern:
    1. Register webhook via pushNotificationConfig/set
    2. Send message/send with the task
    3. Poll M365 webhook for received notifications
    """
    section(f"PUSH — message/send + webhook notification")
    info(f"Query: \"{query}\"")
    info(f"Webhook: {M365_WEBHOOK_URL}")

    # Step 1: Send message/send with inline push notification config
    # The A2A protocol allows embedding pushNotificationConfig in the message
    # configuration so the agent registers the webhook automatically.
    token = f"cli-test-{uuid.uuid4().hex[:8]}"
    params = make_message_params(query)
    params["configuration"]["pushNotificationConfig"] = {
        "url": M365_WEBHOOK_URL,
        "token": token,
    }

    info("Step 1: Sending message/send with inline pushNotificationConfig...")
    msg_payload = make_jsonrpc("message/send", params)
    start = time.time()

    resp = await client.post(f"{ADK_AGENT_URL}/", json=msg_payload)
    resp.raise_for_status()
    result = resp.json()

    if "error" in result:
        err(f"message/send error: {json.dumps(result['error'], indent=2)}")
        return result

    task = result.get("result", {})
    task_id = task.get("id", "unknown")
    task_status = task.get("status", {}).get("state", "unknown")
    ok(f"Task created: {task_id[:20]}... (status: {task_status})")
    ok(f"Webhook token: {token}")

    # Step 2 (optional): Explicitly register via tasks/pushNotificationConfig/set
    # This is an alternative approach — the inline config above should suffice,
    # but we also try the explicit RPC for demonstration.
    info("Step 2: Also registering via tasks/pushNotificationConfig/set...")
    push_config_payload = make_jsonrpc("tasks/pushNotificationConfig/set", {
        "taskId": task_id,
        "pushNotificationConfig": {
            "url": M365_WEBHOOK_URL,
            "token": token,
        },
    })

    push_resp = await client.post(f"{ADK_AGENT_URL}/", json=push_config_payload)
    push_resp.raise_for_status()
    push_result = push_resp.json()

    if "error" in push_result:
        warn(f"Explicit pushNotificationConfig/set: {push_result.get('error', {}).get('message', 'error')}")
    else:
        ok("Explicit webhook registration also succeeded")

    elapsed = time.time() - start
    ok(f"Push setup complete in {elapsed:.1f}s")

    # Step 3: Check if notification arrived at M365 webhook
    info("Step 3: Checking M365 webhook for notifications (waiting 3s)...")
    await asyncio.sleep(3)

    try:
        webhook_resp = await client.get(M365_WEBHOOK_URL)
        webhook_data = webhook_resp.json()
        total = webhook_data.get("total", 0)

        if total > 0:
            ok(f"M365 webhook has {total} notification(s):")
            for n in webhook_data.get("notifications", []):
                info(
                    f"Task: {n.get('task_id', '?')[:12]}... "
                    f"| Status: {n.get('status', '?')} "
                    f"| At: {n.get('received_at', '?')}"
                )
                preview = n.get("text_preview", "")
                if preview:
                    info(f"  → {preview[:120]}")
        else:
            warn("No notifications at webhook yet (may still be processing)")
    except Exception as e:
        warn(f"Could not reach M365 webhook: {e}")

    return result


# ── Status Check ──────────────────────────────────────────────────────────────

async def status(client: httpx.AsyncClient):
    """GET M365 webhook — check received push notifications."""
    section("STATUS — Webhook Notifications")

    try:
        resp = await client.get(M365_WEBHOOK_URL)
        resp.raise_for_status()
        data = resp.json()

        total = data.get("total", 0)
        if total == 0:
            warn("No push notifications received yet")
            info("Use 'push <brand>' to trigger one first")
            return

        ok(f"{total} notification(s) received:")
        for i, n in enumerate(data.get("notifications", []), 1):
            print(
                f"\n  {C.CYAN}#{i}{C.END} "
                f"Task: {n.get('task_id', '?')[:16]}... "
                f"| Status: {C.BOLD}{n.get('status', '?')}{C.END} "
                f"| At: {n.get('received_at', '?')}"
            )
            preview = n.get("text_preview", "")
            if preview:
                for line in preview[:300].split("\n"):
                    print(f"     {C.DIM}│{C.END} {line}")

    except Exception as e:
        err(f"Could not reach A2A client webhook at {M365_WEBHOOK_URL}: {e}")
        info("Is the A2A client agent running on port 3978?")


# ── Run All Patterns ─────────────────────────────────────────────────────────

async def run_all(client: httpx.AsyncClient, query: str):
    """Run discover + all 3 A2A patterns sequentially."""
    section("RUNNING ALL 4 PATTERNS")
    info(f"Query: \"{query}\"")
    print()

    await discover(client)
    await ping(client, query)
    await stream(client, query)
    await push(client, query)
    await status(client)

    section("ALL PATTERNS COMPLETE")
    ok("End-to-end A2A test finished!")


# ── Interactive Menu ──────────────────────────────────────────────────────────

async def interactive():
    """Interactive loop for testing."""
    banner()

    async with httpx.AsyncClient(timeout=TIMEOUT) as client:
        # Quick connectivity check
        try:
            resp = await client.get(f"{ADK_AGENT_URL}/.well-known/agent-card.json")
            resp.raise_for_status()
            card = resp.json()
            ok(f"ADK agent connected: {card['name']}")
        except Exception as e:
            err(f"Cannot reach ADK agent at {ADK_AGENT_URL}: {e}")
            err("Start the ADK agent first: cd adk-agent && poetry run python run_a2a.py")
            return

        try:
            resp = await client.get(M365_WEBHOOK_URL)
            ok(f"M365 webhook reachable")
        except Exception:
            warn(f"M365 webhook not reachable at {M365_WEBHOOK_URL} (push mode won't store notifications)")

        print(f"\n{C.BOLD}Commands:{C.END}")
        print(f"  {C.CYAN}discover{C.END}              — Show agent card")
        print(f"  {C.CYAN}ping <brand> [cat]{C.END}    — Synchronous message/send")
        print(f"  {C.CYAN}stream <brand> [cat]{C.END}  — SSE message/stream")
        print(f"  {C.CYAN}push <brand> [cat]{C.END}    — Push notification + webhook")
        print(f"  {C.CYAN}status{C.END}                — Check webhook notifications")
        print(f"  {C.CYAN}all <brand> [cat]{C.END}     — Run all patterns")
        print(f"  {C.CYAN}quit{C.END}                  — Exit")
        print()

        while True:
            try:
                raw = input(f"{C.BOLD}a2a>{C.END} ").strip()
            except (EOFError, KeyboardInterrupt):
                print("\nBye!")
                break

            if not raw:
                continue

            parts = raw.split(maxsplit=1)
            cmd = parts[0].lower()
            arg = parts[1] if len(parts) > 1 else ""

            if cmd in ("quit", "exit", "q"):
                print("Bye!")
                break
            elif cmd == "discover":
                await discover(client)
            elif cmd == "ping":
                if not arg:
                    warn("Usage: ping <brand> [category]")
                else:
                    await ping(client, arg)
            elif cmd == "stream":
                if not arg:
                    warn("Usage: stream <brand> [category]")
                else:
                    await stream(client, arg)
            elif cmd == "push":
                if not arg:
                    warn("Usage: push <brand> [category]")
                else:
                    await push(client, arg)
            elif cmd == "status":
                await status(client)
            elif cmd == "all":
                if not arg:
                    warn("Usage: all <brand> [category]")
                else:
                    await run_all(client, arg)
            else:
                warn(f"Unknown command: {cmd}")
                info("Type 'help' or see commands above")


# ── CLI Entry Point ───────────────────────────────────────────────────────────

async def main():
    if len(sys.argv) < 2:
        await interactive()
        return

    cmd = sys.argv[1].lower()
    arg = " ".join(sys.argv[2:]) if len(sys.argv) > 2 else ""

    async with httpx.AsyncClient(timeout=TIMEOUT) as client:
        banner()
        if cmd == "discover":
            await discover(client)
        elif cmd == "ping":
            await ping(client, arg or "Nike socks")
        elif cmd == "stream":
            await stream(client, arg or "Nike socks")
        elif cmd == "push":
            await push(client, arg or "Nike socks")
        elif cmd == "status":
            await status(client)
        elif cmd == "all":
            await run_all(client, arg or "Nike socks")
        else:
            err(f"Unknown command: {cmd}")
            print("Commands: discover, ping, stream, push, status, all")


if __name__ == "__main__":
    asyncio.run(main())
