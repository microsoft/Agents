# Enhanced task completion for Microsoft Copilot Studio

*Preview — This feature is currently experimental and is not intended for production use. Before enabling, review the current limitations and risks.*

This feature is for Microsoft Copilot Studio.

Enhanced task completion gives your agent the ability to work through complex tasks the way a human assistant would — by gathering information, asking clarifying questions, and taking action only when it has what it needs.

With standard generative orchestration, your agent selects the tools it needs up front, asks for any missing inputs one at a time, runs those tools, and then returns a single response. This works well for straightforward requests, but can feel rigid when a task requires back-and-forth conversation or when one tool’s output determines what to do next.

With enhanced task completion, your agent can:

- Ask clarifying questions before acting — rather than selecting a tool and then prompting for each missing input individually, your agent can ask natural follow-up questions (including asking for multiple pieces of information at once) and only call a tool once it’s confident it has everything it needs.

- Adapt its plan as it goes — instead of committing to a fixed set of tools up front, your agent can adjust its approach based on what it learns during the conversation. For example, if a tool returns unexpected results, the agent can decide to call a different tool or ask the user for more context.

- Chain tools intelligently — your agent can better recognize when one tool’s output provides the inputs needed for another, calling tools in sequence to build up a complete answer without requiring the user to manually provide information that’s already available. When multiple tools can run independently, your agent can call them in parallel to complete tasks faster.

- Follow instructions more closely — your agent is better able to adhere to the instructions you’ve provided, resulting in more consistent and predictable behavior that aligns with your intended agent design.

- Recover from errors gracefully — if a tool call fails or returns an error, your agent can intelligently retry the call or consider an alternative approach to complete the task, rather than stopping with an error message.

- Carry on a natural conversation throughout — your agent can interleave questions, tool calls, and responses fluidly across multiple turns, rather than following a rigid plan-then-execute pattern.

*Important: This feature is currently experimental and is not intended for production use. Not all features are supported when this feature is enabled. Review the unsupported features before enabling.*

## Enable enhanced task completion

- Open your agent in Copilot Studio.

- Select Settings in the top navigation bar.

- Select Generative AI from the left menu.

- Under Orchestration, find Enhanced task completion and toggle it to On.

- A confirmation dialog appears, listing the features that are not yet supported when this mode is enabled. Review the list, then select Confirm to enable the feature.

## Unsupported features

The following features are not currently available when enhanced task completion is enabled:

- Topics (including topic triggers - e.g. On message received)
- Knowledge / orchestration
- Disable ungrounded responses (general knowledge off)
- Content moderation levels
- Agents
- Child agents
- Message passthrough (all responses from the connected agent go through the calling agent)
- Conversation history sharing for Copilot Studio connected agents
- External agents (Foundry, M365 SDK, A2A protocol)
- Additional languages (primary agent language only currently supported)
- Evaluation
- Analytics
- Activity history
- Tool configuration
- End user confirmation before running a tool
- Override input with formula / static value
- Advanced input configuration (e.g. prompt override, retry count)

*Note: If your agent relies on any of the above features, they won’t be available while enhanced task completion is turned on. You can disable the feature at any time to restore access to these capabilities.*

## Test your agent

When enhanced task completion is enabled, a new test experience is available that combines the activity map and the test chat into a single, unified view.

As your agent processes a request, each tool call appears inline within the conversation. You can expand any tool call to inspect:

- Parameters — the inputs that were sent to the tool.
- Result — the output returned by the tool.
This makes it straightforward to trace how your agent planned and executed each step of a complex task.

By default, tool calls are shown expanded in the conversation. If you prefer them to be collapsed, select the … menu at the top of the test panel and toggle Show detailed reasoning when testing to Off.

## Current limitations and risks

Enhanced task completion is an experimental feature that enhances the agent’s ability to complete multi-step tasks on behalf of the user. This feature is currently in experimental preview and should not be used for production workloads.

Because enhanced task completion is still under active development, there is currently an increased risk of cross-prompt injection attacks (XPIA), where malicious instructions embedded in document content or external data could influence the agent’s behavior. To reduce this risk:

- Carefully review and limit the tools and knowledge sources available to the agent, granting only the minimum capabilities required for your testing.
- Avoid connecting tools that perform irreversible or high-impact actions.
- When testing, monitor agent activity and the information used by the agent using the tool / knowledge observability in the test experience