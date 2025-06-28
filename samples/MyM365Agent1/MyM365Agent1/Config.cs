namespace MyM365Agent1
{
    public class ConfigOptions
    {
        public OpenAIConfigOptions OpenAI { get; set; }
    }

    /// <summary>
    /// Options for Open AI
    /// </summary>
    public class OpenAIConfigOptions
    {
        public string ApiKey { get; set; }
        public string DefaultModel = "gpt-3.5-turbo";
    }
}