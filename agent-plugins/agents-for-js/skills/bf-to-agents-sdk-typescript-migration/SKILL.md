---
name: bf-to-agents-sdk-typescript-migration
description: Assess and migrate JavaScript or TypeScript bots from BotBuilder/Bot Framework SDK to Microsoft 365 Agents SDK with AgentApplication. Use to produce a migration report and plan before editing code, map BotBuilder packages and APIs, identify unsupported features, and implement only the supported plan items selected by the user.
disable-model-invocation: true
---

# BotBuilderJS to Agents SDK migration

Target `AgentApplication` and preserve observable behavior. Always assess and report before editing. Use [migration-map.md](./references/migration-map.md) as the primary reference; search it for each detected package, class, method, property, or handler.

## Workflow

1. Inspect the bot's packages, imports, entry points, handlers, state, middleware, hosting, authentication, tests, and deployment files.
2. Inventory every Bot Framework symbol and behavior. Classify each item with the migration map:
   - **Direct**: replace the package or API while preserving the design.
   - **Rewrite**: implement the supported `AgentApplication` pattern.
   - **Decision**: verified SDK-supported choices exist, but the user must select one.
   - **Unsupported**: no verified stable Agents SDK for JavaScript equivalent exists.
3. Produce the migration plan in the format below. Do not edit project code during this assessment phase.
4. Stop and ask which numbered **Direct** and **Rewrite** items to implement and which **Decision** choices to make. Exclude **Unsupported** items from implementation.
5. After explicit selection, confirm the assessed files, dependencies, and scope have not changed. If they changed, refresh the plan and obtain a new selection. Otherwise, implement only the selected items and update the lockfile.
6. Run the project's build/type-check, lint, and tests. Start the service and exercise the message endpoint and affected custom, invoke, storage, and proactive paths when possible.
7. Report migrated items, decisions still required, unsupported behavior left unchanged, and validation results.

If multiple bots are present and scope is unclear, ask which bot to assess before step 2.

## Rules

- Do not invent an SDK API, shim, placeholder, or unrelated replacement to make the migration appear complete. Mark behavior without a verified stable target as **Unsupported**.
- Treat the migration map as candidate mappings. Verify every **Direct** and **Rewrite** target against declarations for the exact stable package version planned for the project. Repository-main code and official samples may identify candidates but do not prove stable availability; repo-only or `@next` functionality is **Decision**, and functionality with no verified package target is **Unsupported**.
- Leave **Unsupported** code and its dependencies unchanged. Discuss a replacement only as a separate modernization decision, never as an Agents SDK migration mapping.
- Implement a **Decision** item only after the user selects a verified SDK-supported option.
- Preserve exports, module system, endpoint paths, middleware order, route precedence, invoke responses, state keys, storage identifiers, OAuth connection names, and Teams command IDs unless the user approves a change.
- `AgentApplication` selects the first matching route. Put specific routes before catch-alls and merge behavior that previously chained through `next()`.
- Remove a Bot Framework dependency only when no retained import or runtime use remains. **Unsupported** code and dependencies always remain unchanged within this skill. **Decision** code keeps its dependency until the user selects a verified supported migration; deletion or isolation is a separate task outside this skill.
- Prefer compatible stable package versions and preserve the package manager. Classify functionality available only in `@next` as **Decision** and ask before using it.
- Keep production authentication enabled and never expose secrets.
- Change only what is necessary to install, build, test, and run the selected migration items.

## Migration plan format

```md
## Summary
Direct: N · Rewrite: N · Decision: N · Unsupported: N

## Migration plan
| ID | Status | Source | Target or required choice | Files | Notes |
|---|---|---|---|---|---|
| M1 | Direct/Rewrite/Decision/Unsupported | `package: symbol` | `package: symbol`, supported choice, or `No stable SDK equivalent` | affected files | brief behavior constraint |

## Validation plan
- `command or runtime path`: behavior to verify
```

For every **Unsupported** row, state the affected behavior and why it cannot be migrated. End with: `Which Direct/Rewrite IDs should I implement? Unsupported items will remain unchanged.`

## Sources

- https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/bf-migration-nodejs
- https://github.com/microsoft/Agents-for-js
- https://github.com/microsoft/Agents/tree/main/samples/nodejs
- Node.js version: https://github.com/microsoft/Agents-for-js/blob/main/package.json
