# Activity Schema Validator (JavaScript)

This directory contains a lightweight validation script for the Activity Protocol schema. It compiles the JSON Schema with Ajv and validates all embedded JSON examples found in `activity-examples.md`.

## Contents

- `validate-activity.js` – Loads `activity-protocol.schema.json`, extracts JSON code blocks from `activity-examples.md`, and validates each example.
- `README.md` – This documentation.

## Prerequisites

- Node.js 18+ (supports ES Modules & top-level features used here)
- Dependencies: `ajv`, `ajv-formats`

Install (from the repo root or this folder):

```bash
npm install ajv ajv-formats --save-dev
```

> If a top-level `package.json` already exists (it does in this repo), you can just install once at the root and run the script from this subfolder.

## Running the Validator

From this directory:

```bash
node validate-activity.js
```

You can also run from the repo root:

```bash
node specs/activity/schema/validator/js/validate-activity.js
```

Exit codes:
- `0` – All examples are valid.
- `1` – At least one example failed validation (errors printed to stderr).

## How It Works

1. Reads the schema at `../../activity-protocol.schema.json`.
2. Compiles it with Ajv (draft-07, `discriminator` support enabled, all errors collected).
3. Reads `../../activity-examples.md` and extracts every fenced block marked as:
   ```
   ```json
   { ... }
   ```
   ```
4. Parses each block to an object (ignoring blocks that don't parse).
5. Validates each example and prints a per-example result plus a final summary.


## Modifying the Schema

When adding a new Activity type:

1. Create a new definition under `definitions` (e.g., `"myCustomActivity"`).
2. Give it a `properties.type.const` with its unique discriminator value.
3. Add a `$ref` to it in the top-level `oneOf` array.
4. Re-run the validator to ensure examples still pass.

For additional message-like variants:

- Reuse the shared `messageBase` (introduced to avoid conflicting `const` inheritance) via:
  ```json
  "allOf": [ { "$ref": "#/definitions/messageBase" } ]
  ```
  then add a `type.const` specific to your new variant.

## Extending Example Extraction

If you want to also validate examples stored as separate `.json` files, you could enhance `validate-activity.js`:

```js
// Pseudocode addition
const examplesDir = path.join(__dirname, '../../examples');
if (fs.existsSync(examplesDir)) {
  for (const f of fs.readdirSync(examplesDir)) {
    if (f.endsWith('.json')) {
      examples.push(JSON.parse(fs.readFileSync(path.join(examplesDir,f), 'utf8')));
    }
  }
}
```

## Troubleshooting

| Problem | Likely Cause | Fix |
|---------|--------------|-----|
| `MODULE_NOT_FOUND: ajv` | Dependencies not installed | Run `npm install ajv ajv-formats` |
| All examples suddenly failing | Schema syntax error | Validate schema JSON (e.g. with an online validator) |
| Discriminator not selecting subtype | Missing `type.const` or absent subtype in `oneOf` | Add const + add `$ref` in `oneOf` |
| ES Module import error | Older Node.js or `type` not set | Run with Node 18+; package root may need `{ "type": "module" }` |

## License

See the repository's top-level `LICENSE` (MIT).

## Contributing

Open a PR with schema changes plus updated examples. Ensure `node validate-activity.js` returns success.

---
Generated documentation.
