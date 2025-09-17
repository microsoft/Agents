using MyProject.Models;
using System.Text.Json;
using Newtonsoft.Json.Schema;
using NewtonsoftJson = Newtonsoft.Json;
using NewtonsoftJObject = Newtonsoft.Json.Linq.JObject;

namespace MyProject.Services
{
    public class ActivityProtocolValidator
    {
        private readonly JSchema _activitySchema;

        public ActivityProtocolValidator()
        {
            _activitySchema = LoadSchema();
        }

        public ActivityValidationResult ValidateActivity(string jsonInput)
        {
            var result = new ActivityValidationResult();

            try
            {
                if (string.IsNullOrWhiteSpace(jsonInput))
                {
                    result.Errors.Add("JSON input cannot be null or empty");
                    result.ValidationMessage = "Input validation failed: null or empty JSON";
                    return result;
                }

                // First, validate JSON format using System.Text.Json
                JsonDocument jsonDoc;
                try
                {
                    jsonDoc = JsonDocument.Parse(jsonInput);
                }
                catch (JsonException ex)
                {
                    result.Errors.Add($"Invalid JSON format: {ex.Message}");
                    result.ValidationMessage = "JSON parsing error";
                    return result;
                }

                // Extract activity type if present
                if (jsonDoc.RootElement.TryGetProperty("type", out JsonElement typeElement))
                {
                    result.ActivityType = typeElement.GetString();
                }

                // Convert to Newtonsoft.Json JObject for schema validation
                var newtonsoftJsonObject = NewtonsoftJObject.Parse(jsonInput);

                // Validate against schema
                IList<string> errorMessages;
                bool isValid = newtonsoftJsonObject.IsValid(_activitySchema, out errorMessages);

                result.IsValid = isValid;
                result.Errors = errorMessages.ToList();

                if (isValid)
                {
                    result.ValidationMessage = $"Activity validation successful for type: {result.ActivityType ?? "unknown"}";
                }
                else
                {
                    result.ValidationMessage = $"Activity validation failed for type: {result.ActivityType ?? "unknown"}";
                }

                jsonDoc.Dispose();
            }
            catch (NewtonsoftJson.JsonException ex)
            {
                result.Errors.Add($"Invalid JSON format: {ex.Message}");
                result.ValidationMessage = "JSON parsing error";
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Validation error: {ex.Message}");
                result.ValidationMessage = "Unexpected validation error";
            }

            return result;
        }

        public async Task<ActivityValidationResult> ValidateActivityAsync(string jsonInput)
        {
            return await Task.Run(() => ValidateActivity(jsonInput));
        }

        private JSchema LoadSchema(string? customPath = null)
        {
            try
            {
                string schemaPath;

                if (!string.IsNullOrEmpty(customPath))
                {
                    schemaPath = customPath;
                }
                else
                {
                    schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_protocol.schema");
                }

                if (!File.Exists(schemaPath))
                {
                    throw new FileNotFoundException($"Activity protocol schema file not found at: {schemaPath}");
                }

                var schemaJson = File.ReadAllText(schemaPath);
                return JSchema.Parse(schemaJson);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load activity protocol schema: {ex.Message}", ex);
            }
        }
    }
}