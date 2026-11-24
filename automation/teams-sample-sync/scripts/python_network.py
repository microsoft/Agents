"""Fail protected execution on unexpected network traffic; allow loopback fixtures."""

import ipaddress
import os
import sys

VIOLATIONS = []
TERMINATE_ON_VIOLATION = False


def reject(host):
    VIOLATIONS.append(str(host))
    message = f"Unexpected external request: {host}"
    if TERMINATE_ON_VIOLATION:
        # HTTP handlers may catch exceptions; terminate the owned test host so
        # readiness cannot certify an application that attempted external IO.
        print(message, file=sys.stderr, flush=True)
        os._exit(1)
    raise RuntimeError(message)


def loopback(host):
    if isinstance(host, bytes):
        host = host.decode("ascii")
    if host == "localhost":
        return True
    try:
        return ipaddress.ip_address(host).is_loopback
    except ValueError:
        return False


def network_event(event, args):
    if event == "socket.getaddrinfo":
        host = args[0]
    elif event in {"socket.connect", "socket.sendto"}:
        address = args[1] if event == "socket.connect" else args[-1]
        if not isinstance(address, tuple):
            reject("non-IP socket")
        host = address[0]
    else:
        return
    if not loopback(host):
        reject(host)


def restrict_network(terminate=False):
    global TERMINATE_ON_VIOLATION
    TERMINATE_ON_VIOLATION = TERMINATE_ON_VIOLATION or terminate
    sys.addaudithook(network_event)


def assert_no_external_requests():
    if VIOLATIONS:
        raise ValueError(f"Unexpected external requests occurred: {VIOLATIONS}")
