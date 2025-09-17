namespace MyProject.Models
{
    public class ActivityValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string? ActivityType { get; set; }
        public string? ValidationMessage { get; set; }
    }
}
