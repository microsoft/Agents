# Prompty-Based Agent Architecture with Few-Shot Learning

## Overview

This project demonstrates an alternative approach to building AI agents that work with **any instruction-following model**, not just those that support OpenAI-style function calling. Instead of relying on complex function calling mechanisms, we use **Prompty templates with few-shot learning examples** to achieve the same results with better performance and reliability.

## The Problem We Solved

### Traditional Approach (ChatCompletionAgent + Function Calling)
- **Issue**: Models like Codestral don't properly support OpenAI-style function calling
- **Symptoms**: 
  - 100+ second timeouts
  - Models return text descriptions of function calls instead of structured `tool_calls`
  - Complex agent loops that hang or fail
  - Dependency on specific model capabilities

### Our Solution (Prompty + Few-Shot Learning)
- **Approach**: Use Prompty templates with examples to teach the model the desired behavior
- **Benefits**:
  - ⚡ **Fast**: 15-20 seconds vs 100+ second timeouts
  - 🛡️ **Reliable**: No hanging or complex agent loops
  - 🔄 **Model-agnostic**: Works with any instruction-following model
  - 🎯 **Direct**: Single LLM call instead of multi-step agent orchestration
  - 📝 **Maintainable**: Clear examples in Prompty files

## Architecture

### Core Components

1. **Prompty Template** (`Prompts/weather-forecast.prompty`)
   - Contains few-shot examples showing desired input/output patterns
   - Uses Jinja2 templating for dynamic content
   - Teaches the model through examples, not function schemas

2. **Manual Intent Detection** (`WeatherForecastAgent.cs`)
   - C# code analyzes user input for weather-related keywords
   - Extracts location using regex patterns
   - Determines when to call plugins

3. **Direct Plugin Orchestration**
   - Agent code calls plugins directly based on detected intent
   - No dependency on LLM to make function calling decisions
   - Results passed as template variables to Prompty

### Flow Diagram

```
User Input: "What's the weather in Seattle today?"
     ↓
[Intent Detection] → Detects: weather query, location="Seattle"
     ↓
[Plugin Orchestration] → Calls: DateTimePlugin.Today(), GetWeatherData()
     ↓
[Prompty Template] → Uses examples + data → Generates response
     ↓
Output: "Hi! The weather in Seattle today is 65°F with sunshine..."
```

## Implementation Details

### Prompty Template Structure

```yaml
---
name: WeatherForecastPrompt
description: A weather forecast assistant using few-shot learning
model:
  api: chat
  parameters:
    max_tokens: 1000
    temperature: 0.7

sample:
  user_request: "What's the weather like in Seattle today?"
  current_date: "Friday, June 28, 2025"
  weather_data: "Temperature: 72°F, Condition: Partly Cloudy"
  location: "Seattle"
---

system:
You are a friendly weather forecast assistant.

Example 1 - Weather data available:
User: "What's the weather like in Seattle today?"
Response:
{
    "contentType": "Text",
    "content": "Hi! The weather in Seattle today is quite nice! 🌤️..."
}

Example 2 - No weather data:
User: "What's the weather like in Paris?"
Response:
{
    "contentType": "Text", 
    "content": "I'd be happy to help! Could you specify the date?"
}

Current Date: {{current_date}}
Weather Data: {{weather_data}}
Location: {{location}}

user:
{{user_request}}
```

### Agent Implementation Pattern

```csharp
public class WeatherForecastAgent
{
    private readonly Kernel _kernel;
    private readonly KernelFunction _weatherFunction;
    private readonly DateTimePlugin _dateTimePlugin;

    public WeatherForecastAgent(Kernel kernel, IServiceProvider service)
    {
        _kernel = kernel;
        
        // Load Prompty template instead of ChatCompletionAgent
        var fileProvider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
        _weatherFunction = _kernel.CreateFunctionFromPromptyFile(
            "Prompts/weather-forecast.prompty", fileProvider);
            
        _dateTimePlugin = new DateTimePlugin();
    }

    public async Task<WeatherForecastAgentResponse> InvokeAgentAsync(string input, ChatHistory chatHistory)
    {
        // 1. Manual intent detection
        bool isWeatherQuery = IsWeatherQuery(input);
        string location = ExtractLocation(input);
        
        // 2. Direct plugin orchestration
        string currentDate = _dateTimePlugin.Today();
        string weatherData = isWeatherQuery && !string.IsNullOrEmpty(location) 
            ? GetWeatherData(location, currentDate) 
            : "No data available";
        
        // 3. Single Prompty call with template variables
        var arguments = new KernelArguments()
        {
            ["user_request"] = input,
            ["current_date"] = currentDate,
            ["weather_data"] = weatherData,
            ["location"] = location
        };
        
        var result = await _kernel.InvokeAsync(_weatherFunction, arguments);
        return ParseResponse(result.ToString());
    }
}
```

## Performance Comparison

| Approach | Response Time | Reliability | Model Support | Complexity |
|----------|---------------|-------------|---------------|------------|
| **ChatCompletionAgent + Function Calling** | 100+ seconds (timeout) | ❌ Unreliable | ⚠️ Function-calling models only | 🔴 High |
| **Prompty + Few-Shot Learning** | 15-20 seconds | ✅ Reliable | ✅ Any instruction-following model | 🟢 Low |

## Key Insights

### Why This Works Better

1. **Few-Shot Learning is Powerful**: Models learn from examples better than from function schemas
2. **Intent Detection in Code**: More reliable than LLM-based intent detection
3. **Single LLM Call**: Eliminates complex multi-turn agent loops
4. **Template Variables**: Clean separation between data and presentation logic
5. **Model Agnostic**: Works with Codestral, Llama, Mistral, GPT, etc.

### When to Use This Pattern

✅ **Use Prompty + Few-Shot when:**
- Working with local/open-source models
- Model doesn't support function calling well
- Need fast, reliable responses
- Want simpler debugging and maintenance
- Working with instruction-following models

❌ **Stick with ChatCompletionAgent when:**
- Using GPT-4/GPT-3.5 with perfect function calling
- Need complex multi-agent orchestration
- Function calling is core to your architecture

## Files Modified

- `Bot/Agents/WeatherForecastAgent.cs` - Refactored to use Prompty approach
- `Prompts/weather-forecast.prompty` - Few-shot learning template
- `MyM365Agent1.csproj` - Added Microsoft.SemanticKernel.Prompty package

## Testing Results

**Before (ChatCompletionAgent):**
```
[3:32:00] Weather query started
[3:33:01] Client disconnected. Stopping generation...
[3:33:01] Timeout after 100+ seconds
```

**After (Prompty + Few-Shot):**
```
[3:57:42] Weather query started  
[3:58:01] Response: "Hi! The weather in Seattle today is 65°F with sunshine..."
[3:58:01] Success in ~19 seconds
```

## Future Enhancements

1. **Real Weather API Integration**: Replace simulated weather data
2. **More Complex Intent Detection**: Handle multi-intent queries
3. **Adaptive Card Support**: Re-enable rich card formatting
4. **Multi-Domain Examples**: Extend pattern to other domains beyond weather
5. **Dynamic Example Selection**: Choose examples based on query type

## Conclusion

This approach represents an alternative to **model-dependent function calling** by using **model-agnostic few-shot learning**. It's faster, more reliable, and works with a broader range of models while being easier to maintain and debug.

The key insight: **Don't fight the model's limitations - work with its strengths**. Most models excel at following examples and patterns, even if they struggle with complex function calling protocols.
