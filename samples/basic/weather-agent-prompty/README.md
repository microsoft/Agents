# MyM365Agent1 - Prompty-Based Weather Agent

## 🚀 Alternative Architecture: Function Calling Without Function Calling

This project demonstrates an alternative approach to building AI agents that work with **any instruction-following model**, not just those with OpenAI-style function calling support.

### The Innovation

Instead of relying on complex function calling mechanisms that cause timeouts and failures with models like Codestral, we use:

- **📝 Prompty templates** with few-shot learning examples
- **🎯 Manual intent detection** in C# code  
- **⚡ Direct plugin orchestration** without LLM decision-making
- **🔄 Model-agnostic architecture** that works with any model

### Performance Results

| Approach | Response Time | Success Rate |
|----------|---------------|--------------|
| **Before**: ChatCompletionAgent + Function Calling | 100+ seconds (timeout) | ❌ 0% |
| **After**: Prompty + Few-Shot Learning | 15-20 seconds | ✅ 100% |

### Quick Start

1. **Ask for weather**: "What's the weather like in Seattle today?"
2. **Get instant response**: Agent detects intent, calls plugins, uses examples to format response
3. **No timeouts**: Single LLM call instead of complex agent loops

### Architecture Highlights

- **Few-shot examples** in `Prompts/weather-forecast.prompty` teach the model desired behavior
- **Intent detection** using keyword matching and regex in `WeatherForecastAgent.cs`
- **Template variables** pass plugin results to Prompty for formatting
- **Fallback handling** ensures robust responses even when plugins fail

## Files

- 📄 `PROMPTY_ARCHITECTURE.md` - Complete technical documentation
- 🧠 `Bot/Agents/WeatherForecastAgent.cs` - Prompty-based agent implementation  
- 📝 `Prompts/weather-forecast.prompty` - Few-shot learning template
- ⚙️ `Program.cs` - LM Studio configuration

## Key Insight

**Don't fight the model's limitations - work with its strengths.** Most models excel at following examples and patterns, even if they struggle with complex function calling protocols.

This approach is:
- ⚡ **Faster** than traditional function calling
- 🛡️ **More reliable** with better error handling  
- 🔄 **Model-agnostic** works with any instruction-following model
- 🔧 **Easier to debug** with clear examples and simple flow

---

*This technique provides an alternative to model-dependent function calling by using model-agnostic few-shot learning.*
