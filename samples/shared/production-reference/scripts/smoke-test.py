"""Anonymous probes and authentication rejection; channel delivery is a separate check."""
import argparse
import json
from urllib.error import HTTPError
from urllib.request import Request, urlopen


def status(request):
    try:
        with urlopen(request, timeout=10) as response:
            return response.status
    except HTTPError as error:
        return error.code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base_url", help="HTTPS App Service or selected edge URL")
    args = parser.parse_args()
    base = args.base_url.rstrip("/")
    checks = [
        ("liveness", Request(f"{base}/health/live"), 200),
        ("readiness", Request(f"{base}/health/ready"), 200),
        ("missing JWT", Request(
            f"{base}/api/messages",
            data=json.dumps({"type": "message", "channelId": "webchat", "serviceUrl": "https://webchat.botframework.com/"}).encode(),
            headers={"Content-Type": "application/json"}, method="POST",
        ), 401),
    ]
    for label, request, expected in checks:
        actual = status(request)
        if actual != expected:
            raise SystemExit(f"{label}: expected {expected}, received {actual}")
        print(f"{label}: passed ({actual})")


if __name__ == "__main__":
    main()
