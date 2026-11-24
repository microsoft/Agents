# Teams feature migration in JavaScript and TypeScript

Inspect the selected installed packages before choosing signatures. The examples
below reflect the repository's 1.9.1 JS samples; other versions require verification.

## Lifecycle and message dispatch

Inventory installation, membership, team/channel and message-update events when
present. Preserve action filters: an install-only handler must not execute for
removal or upgrade events. Verify the JS registration helper and payload rather
than translating a .NET attribute name. Test each relevant action through dispatch.
Preserve exact versus substring matching, case sensitivity, mention handling,
attachment-only messages, fallback behavior and overlapping-command precedence.

## AI-generated content and feedback

Use the JS SDK's supported helpers where available, such as `addAIToActivity` and
`teamsEnableFeedbackLoop`. Register feedback handling through the selected Teams
extension. Verify feedback parsing for each source-supported payload format.
For sensitivity and citations, preserve the serialized schema.org message entities,
usageInfo, citation positions, source URLs and embedded card content. JS structured
entities can be valid; the .NET rule to use particular CLR model classes does not
transfer. Do not move required metadata into arbitrary channelData or stringify
an entire entity. Test the serialized outgoing activity and invoke acknowledgment.

## Graph authentication

Preserve whether the source calls Graph on behalf of a user or as the application.
In the selected JS SDK, TeamsTurnContext exposes `getGraphClient` for delegated
access and `getAppGraphClient` for app-only access through the turn's connection.
`getAppGraphClientForConnection` selects a named application connection. Verify
these APIs against the installed types; do not copy .NET's independent-client rule.
Preserve the original Graph permissions and endpoint version. Meeting transcript
lookup needs the Graph meeting ID and organizer identity, not a substituted Teams
meeting ID. Mocked tests do not prove tenant consent or application access policy.

## Long-running invokes

Inspect when the SDK acknowledges the invoke. In 1.9.1, fileConsent.onAccept awaits
its callback before emitting the invoke response. Sending an ordinary message
from that callback does not acknowledge the invoke.

Queue lengthy uploads and acknowledge without waiting for the transfer. Capture
immutable payload data, a cloned conversation reference and the adapter; do not
capture the original turn context in background work. Use Agents proactive
continuation for completion messages. Track queued work and handle its errors;
an unobserved promise is not a queue. Preserve one-time consent consumption and
cleanup on both success and failure. Document in-memory queue restart limitations.
Test with an intentionally blocked upload: acknowledgment must complete first,
then upload completion must send through a new proactive turn. Test replay and
failure behavior as well. Production durability follows the application's needs.
