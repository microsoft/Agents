# Contributing to Agent Plugins

This guide explains how to add new skills, update existing skills, and add new plugins to this marketplace.

---

## How It's Organized

```
.claude-plugin/marketplace.json          ← top-level marketplace registry
agent-plugins/
  agents-for-js/                         ← a plugin
    plugin.json                          ← plugin metadata
    skills/
      <skill-name>/
        SKILL.md                         ← the skill content
```

The **marketplace registry** (`/.claude-plugin/marketplace.json`) lists the plugins available for install. Each **plugin** groups related skills for a language or platform. Each **skill** is a Markdown file with a YAML frontmatter block that tells the AI assistant when to activate it.

---

## Adding a New Skill to an Existing Plugin

1. **Create a directory** under the plugin's `skills/` folder. Name it after the skill using lowercase and hyphens:

   ```
   agent-plugins/agents-for-js/skills/my-new-skill/
   ```

2. **Create `SKILL.md`** in that directory with a YAML frontmatter block:

   ```markdown
   ---
   name: my-new-skill
   description: Use when [trigger condition that activates this skill]
   ---

   # Skill Title

   Skill content here...
   ```

3. **Write a precise `description`** — this is the trigger condition the AI uses to decide when to load the skill. Be specific:
   - Good: `Use when any code imports @microsoft/agents-hosting or when building a new agent`
   - Too vague: `Use for agents`
   - Too broad: `Use when working with JavaScript`

4. **No registration needed** — skills are auto-discovered from the `skills/` directory via the `"skills": "skills/"` entry in `plugin.json`.

---

## Updating an Existing Skill

Edit the `SKILL.md` file directly. There is no build step — changes take effect the next time the plugin is loaded.
[Updating the version](#versioning) will cause most agentic clients to update the skill automatically.

---

## Adding a New Plugin

A plugin groups skills for a new language, platform, or use case (e.g., a `agents-for-dotnet` plugin).

1. **Create a plugin directory** under `agent-plugins/`:

   ```
   agent-plugins/agents-for-dotnet/
   ```

2. **Add `plugin.json`**:

   ```json
   {
     "name": "agents-for-dotnet",
     "description": "Skills for building agents with the Microsoft 365 Agents SDK for .NET",
     "version": "1.0.0",
     "author": {
       "name": "Microsoft"
     },
     "license": "MIT",
     "keywords": ["microsoft", "agents", "teams", "dotnet"],
     "skills": "skills/"
   }
   ```

3. **Add skills** following the steps in [Adding a New Skill](#adding-a-new-skill-to-an-existing-plugin).

4. **Register the plugin** in `/.claude-plugin/marketplace.json` by adding an entry to the `plugins` array:

   ```json
   {
     "name": "agents-for-dotnet",
     "source": "./agent-plugins/agents-for-dotnet",
     "description": "Skills for building agents with the Microsoft 365 Agents SDK for .NET",
     "version": "1.0.0"
   }
   ```

5. **Document it** in `agent-plugins/README.md` — add the plugin to the Available Plugins table and list its skills.

---

## Versioning

Bump the `version` field in `plugin.json` when making significant changes to a plugin's skills. This helps users know when to reinstall.
