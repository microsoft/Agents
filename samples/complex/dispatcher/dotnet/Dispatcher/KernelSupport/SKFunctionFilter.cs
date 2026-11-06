using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace DispatcherAgent.KernelSupport
{
    public class SKFunctionFilter : IAutoFunctionInvocationFilter
    {
        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            // Example: get function information
            var functionName = context.Function.Name;

            // Example: get chat history
            var chatHistory = context.ChatHistory;

            // Example: get information about all functions which will be invoked
            var functionCalls = FunctionCallContent.GetFunctionCalls(context.ChatHistory.Last());

            // In function calling functionality there are two loops.
            // Outer loop is "request" loop - it performs multiple requests to LLM until user ask will be satisfied.
            // Inner loop is "function" loop - it handles LLM response with multiple function calls.

            // Workflow example:
            // 1. Request to LLM #1 -> Response with 3 functions to call.
            //      1.1. Function #1 called.
            //      1.2. Function #2 called.
            //      1.3. Function #3 called.
            // 2. Request to LLM #2 -> Response with 2 functions to call.
            //      2.1. Function #1 called.
            //      2.2. Function #2 called.

            // context.RequestSequenceIndex - it's a sequence number of outer/request loop operation.
            // context.FunctionSequenceIndex - it's a sequence number of inner/function loop operation.
            // context.FunctionCount - number of functions which will be called per request (based on example above: 3 for first request, 2 for second request).

            // Example: get request sequence index
            System.Diagnostics.Trace.WriteLine($"Request sequence index: {context.RequestSequenceIndex}");

            // Example: get function sequence index
            System.Diagnostics.Trace.WriteLine($"Function sequence index: {context.FunctionSequenceIndex}");

            // Example: get total number of functions which will be called
            System.Diagnostics.Trace.WriteLine($"Total number of functions: {context.FunctionCount}");

            // Calling next filter in pipeline or function itself.
            // By skipping this call, next filters and function won't be invoked, and function call loop will proceed to the next function.
            await next(context);

            // Example: get function result
            var result = context.Result;

            if (!string.IsNullOrEmpty(context.Function.Name) &&
                    (context.Function.Name.Equals("handle_weatherrequest", StringComparison.OrdinalIgnoreCase) ||
                    context.Function.Name.Equals("handle_cas", StringComparison.OrdinalIgnoreCase)
                    ))
            {
                if (context.Result.ValueType == typeof(bool))
                {
                    var resultValue = context.Result.GetValue<bool>();
                    if (resultValue)
                    {
                        //context.Result = null; 
                        context.Terminate = true;
                    }
                }
            }

            // Example: override function result value
            // context.Result = new FunctionResult(context.Result, "Result from auto function invocation filter");

            // Example: Terminate function invocation
            //context.Terminate = true;
        }
    }
}
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
