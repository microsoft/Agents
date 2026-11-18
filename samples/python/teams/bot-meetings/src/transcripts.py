# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

"""Graph transcript retrieval and WebVTT formatting."""

import logging
import os
import re
from functools import lru_cache
from urllib.parse import urlsplit

from azure.identity.aio import ClientSecretCredential
from kiota_abstractions.base_request_configuration import RequestConfiguration
from msgraph import GraphServiceClient
from msgraph.generated.users.item.online_meetings.online_meetings_request_builder import (
    OnlineMeetingsRequestBuilder,
)
from msgraph.generated.models.call_transcript_collection_response import (
    CallTranscriptCollectionResponse,
)
from msgraph.generated.models.online_meeting_collection_response import (
    OnlineMeetingCollectionResponse,
)

logger = logging.getLogger(__name__)
GRAPH_DEFAULT_BASE_URL = "https://graph.microsoft.com"
GRAPH_DEFAULT_API_VERSION = "beta"


def parse_vtt(text: str) -> str:
    """Return non-empty speaker lines from a WebVTT transcript."""
    lines = []
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("WEBVTT") or "-->" in line:
            continue
        line = re.sub(r"<v ([^>]+)>", r"\1: ", line)
        line = re.sub(r"<[^>]+>", "", line).strip()
        if line:
            lines.append(line)
    return "\n".join(lines)


def _configured_graph_client() -> GraphServiceClient | None:
    tenant_id = os.environ.get("TENANT_ID", "").strip()
    client_id = os.environ.get("CLIENT_ID", "").strip()
    client_secret = os.environ.get("CLIENT_SECRET", "").strip()
    if not all((tenant_id, client_id, client_secret)):
        return None
    return _create_graph_client(tenant_id, client_id, client_secret)


def _absolute_request_url(graph_client: GraphServiceClient, url: str) -> str:
    if urlsplit(url).scheme:
        return url
    base_url = graph_client.request_adapter.base_url.rstrip("/")
    return f"{base_url}/{url.lstrip('/')}"


@lru_cache(maxsize=1)
def _create_graph_client(
    tenant_id: str, client_id: str, client_secret: str
) -> GraphServiceClient:
    credential = ClientSecretCredential(
        tenant_id=tenant_id,
        client_id=client_id,
        client_secret=client_secret,
    )
    client = GraphServiceClient(credential, ["https://graph.microsoft.com/.default"])
    base_url = os.environ.get("GRAPH_BASE_URL", GRAPH_DEFAULT_BASE_URL).rstrip("/")
    api_version = os.environ.get("GRAPH_API_VERSION", GRAPH_DEFAULT_API_VERSION).strip("/")
    client.request_adapter.base_url = f"{base_url}/{api_version}"
    return client


async def resolve_meeting_resource_id(
    graph_client: GraphServiceClient, join_url: str, user_id: str
) -> str | None:
    """Resolve a Graph onlineMeeting ID for this organizer and join URL."""
    if not join_url or not user_id:
        return None

    escaped_join_url = join_url.replace("'", "''")
    query_parameters = (
        OnlineMeetingsRequestBuilder.OnlineMeetingsRequestBuilderGetQueryParameters(
            filter=f"JoinWebUrl eq '{escaped_join_url}'"
        )
    )
    request_configuration = (
        OnlineMeetingsRequestBuilder.OnlineMeetingsRequestBuilderGetRequestConfiguration(
            query_parameters=query_parameters
        )
    )
    request_builder = graph_client.users.by_user_id(user_id).online_meetings
    request_information = request_builder.to_get_request_information(
        request_configuration=request_configuration
    )
    request_information.url = _absolute_request_url(
        graph_client, request_information.url
    )
    meetings = await graph_client.request_adapter.send_async(
        request_information,
        OnlineMeetingCollectionResponse,
        None,
    )
    for meeting in meetings.value if meetings and meetings.value else []:
        if meeting.id:
            return meeting.id
    return None


async def get_meeting_transcript(
    graph_client: GraphServiceClient, resource_id: str, user_id: str
) -> str:
    """Fetch the newest VTT transcript, keeping resource_id as a Graph ID."""
    if not resource_id or not user_id:
        return ""

    transcript_builder = (
        graph_client.users.by_user_id(user_id)
        .online_meetings.by_online_meeting_id(resource_id)
        .transcripts
    )
    metadata_request = transcript_builder.to_get_request_information()
    metadata_request.url = _absolute_request_url(graph_client, metadata_request.url)
    transcripts = await graph_client.request_adapter.send_async(
        metadata_request,
        CallTranscriptCollectionResponse,
        None,
    )
    transcript_items = transcripts.value if transcripts and transcripts.value else []
    newest = max(
        transcript_items,
        key=lambda item: item.created_date_time or "",
        default=None,
    )
    if not newest or not newest.id:
        return ""

    request_configuration = RequestConfiguration()
    request_configuration.headers.add("Accept", "text/vtt")
    content_builder = (
        graph_client.users.by_user_id(user_id)
        .online_meetings.by_online_meeting_id(resource_id)
        .transcripts.by_call_transcript_id(newest.id)
        .content
    )
    content_request = content_builder.to_get_request_information(
        request_configuration=request_configuration
    )
    content_request.url = _absolute_request_url(graph_client, content_request.url)
    content = await graph_client.request_adapter.send_primitive_async(
        content_request,
        bytes,
        None,
    )
    return content.decode("utf-8") if content else ""


async def default_transcript(
    _context: object, resource_id: str, user_id: str
) -> str:
    graph_client = _configured_graph_client()
    if graph_client is None:
        logger.warning("Graph transcript credentials are not configured")
        return ""
    return await get_meeting_transcript(graph_client, resource_id, user_id)


async def default_resolve_meeting(
    _context: object, join_url: str, user_id: str
) -> str | None:
    graph_client = _configured_graph_client()
    if graph_client is None:
        logger.warning("Graph meeting lookup credentials are not configured")
        return None
    return await resolve_meeting_resource_id(graph_client, join_url, user_id)
