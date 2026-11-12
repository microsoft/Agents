using NJsonSchema;
using NJsonSchema.Validation;
using Newtonsoft.Json.Linq;

static async Task<JsonSchema> LoadSchemaAsync(string schemaPath) => await JsonSchema.FromFileAsync(schemaPath);

string schemaFile = Path.Combine(AppContext.BaseDirectory, "activity.schema.json");
if (!File.Exists(schemaFile))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Schema file not found: {schemaFile}");
    Console.ResetColor();
    return;
}

// Discover example files relative to project root (navigate up to validator folder structure)
// Expect examples under ../examples/json
string? baseDir = AppContext.BaseDirectory;
// Try to locate examples directory by walking up a few levels
string? examplesDir = null;
var current = new DirectoryInfo(baseDir);
for (int i = 0; i < 6 && current != null; i++)
{
    var probe = Path.Combine(current.FullName, "examples", "json");
    if (Directory.Exists(probe)) { examplesDir = probe; break; }
    current = current.Parent;
}

if (examplesDir == null)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Examples directory not found (expected ../examples/json). Will fall back to input.json if present.");
    Console.ResetColor();
}

var schema = await LoadSchemaAsync(schemaFile);

List<(string name, JToken instance)> instances = new();

if (examplesDir != null)
{
    foreach (var file in Directory.GetFiles(examplesDir, "*.json", SearchOption.TopDirectoryOnly).OrderBy(f => f))
    {
        try
        {
            var text = File.ReadAllText(file);
            instances.Add((Path.GetFileName(file), JToken.Parse(text)));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to parse {file}: {ex.Message}");
            Console.ResetColor();
        }
    }
}

// Backward compatibility: single input.json
string singleInput = Path.Combine(AppContext.BaseDirectory, "input.json");
if (File.Exists(singleInput))
{
    try
    {
        instances.Add(("input.json", JToken.Parse(File.ReadAllText(singleInput))));
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("input.json parse error: " + ex.Message);
        Console.ResetColor();
    }
}

if (instances.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("No JSON instances found to validate.");
    Console.ResetColor();
    return;
}

int total = 0;
int failures = 0;

foreach (var (name, token) in instances)
{
    total++;
    var errors = schema.Validate(token);
    if (errors.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[OK] {name} ({token["type"] ?? "unknown-type"})");
        Console.ResetColor();
    }
    else
    {
        failures++;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[FAIL] {name} ({token["type"] ?? "unknown-type"}) - {errors.Count} error(s)");
        Console.ResetColor();
        int i = 1;
        foreach (var e in errors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [{i}] {e.Kind}");
            Console.ResetColor();
            Console.WriteLine($"     Path    : {(string.IsNullOrEmpty(e.Path) ? "$" : "$" + e.Path)}");
            Console.WriteLine($"     Detail  : {e}");
            if (!string.IsNullOrEmpty(e.Property))
                Console.WriteLine($"     Property: {e.Property}");
            i++;
        }
    }
}

Console.WriteLine();
Console.WriteLine($"Summary: {total - failures} valid / {failures} invalid / {total} total");
if (failures == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("All examples are valid.");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Some examples failed validation.");
    Console.ResetColor();
    Environment.ExitCode = 1;
}