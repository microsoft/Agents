# Production reference operations

This runbook covers the bounded Web Chat reference in each language. Record an
owner/on-call team, deployment ID, artifact hash, SDK commit/version, resource
group, expected identity, edge configuration and current readiness assessment.

## Promotion evidence

- Run local build/test/lint checks and record command/results.
- Deploy the selected language's artifact and record infrastructure outputs.
- Run the shared smoke script against the selected public entry point.
- Use Azure Bot Test in Web Chat: send an issue summary, then `high`. Verify the
  saved acknowledgment and completed-conversation response on a later turn.
- Restart App Service between summary and impact; confirm recovery. Exercise
  another replica against the same store and test conflicting writes. Use separate
  storage containers per environment/language; state wire formats are not portable.
- Exercise invalid signature, missing/malformed/expired token, wrong audience,
  wrong issuer, disallowed channel/host and `serviceurl` mismatch. Real Azure
  metadata and token acquisition need deployed evidence beyond synthetic tests.
- Inspect exported metrics/spans using synthetic sensitive content. Confirm
  messages, credentials and unnecessary identifiers are absent. Confirm request
  failure/latency visibility, sampling/retention and alert delivery to the owner.
- Configure and test caller-aware traffic protection and origin-bypass prevention.
  The Bicep creates the host, bot, identity, storage and monitoring; it does not
  configure an authenticated edge or alert action groups/rules.
- Exercise shutdown while a turn is in flight. Confirm draining and telemetry flush.
- Redeploy the previous immutable artifact, rerun smoke/channel/state checks and
  record rollback duration. Promote only after outstanding applicable controls pass.

## Incident response

1. Check independent liveness, then storage-dependent readiness. A failed
   readiness probe with healthy liveness usually indicates a required dependency.
2. Inspect bounded failure categories and platform metrics without dumping request
   bodies/tokens. Check Blob RBAC propagation, configured identity and dependency
   availability. Do not enable verbose content-bearing logs on live user traffic.
3. For authentication failures, verify the deployment audience, channel token
   family, issuer/metadata and cloud. Do not temporarily disable JWT validation,
   broaden issuers/hosts or reintroduce development credentials.
4. For state conflicts, preserve the existing item. Ask the user to retry the
   intended step; do not automatically replay a complete turn or erase conversation
   state. Investigate simultaneous turns and duplicate delivery.
5. If a change caused the incident, restore the known compatible artifact.
   Do not roll back readers across an incompatible state schema.

## Retention, deletion and recovery

Conversation state contains the user-supplied issue summary. Record its data
classification, retention period and access/deletion owner before production.
Apply a tested Blob lifecycle/cleanup policy, account for soft-delete/backups,
and verify deletion covers retained copies under the selected policy. Readiness
uses unique `health.readiness.*` objects; delete orphan probe objects after failed
cleanup. Do not confuse probes with customer state.

Document backup/restore scope and recovery time/data-loss objectives. Rehearse
restore into an isolated container and verify schema compatibility before changing
the active configuration. Remove obsolete identities and review storage permissions.
