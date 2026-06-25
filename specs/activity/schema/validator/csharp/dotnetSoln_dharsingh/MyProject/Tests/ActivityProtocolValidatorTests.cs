using MyProject.Services;
using Xunit;

namespace MyProject.Tests
{
    public class ActivityProtocolValidatorTests
    {
        private readonly ActivityProtocolValidator _validator;

        public ActivityProtocolValidatorTests()
        {
            _validator = new ActivityProtocolValidator();
        }

        [Fact]
        public void ValidateActivity_WithValidMessageActivity_ShouldReturnValid()
        {
            // Arrange
            var validMessageJson = """
            {
                "type": "message",
                "id": "msg-001",
                "channelId": "webchat",
                "from": {
                    "id": "user123",
                    "name": "John Doe"
                },
                "conversation": {
                    "id": "conv-456"
                },
                "recipient": {
                    "id": "bot789",
                    "name": "My Bot"
                },
                "serviceUrl": "https://example.com/api",
                "text": "Hello, world!",
                "timestamp": "2025-09-17T10:00:00Z"
            }
            """;

            // Act
            var result = _validator.ValidateActivity(validMessageJson);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal("message", result.ActivityType);
            Assert.Empty(result.Errors);
        }

        [Theory]
        [InlineData("01-message.json")]
        [InlineData("02-contactRelationUpdate.json")]
        [InlineData("03-conversationUpdate.json")]
        [InlineData("04-typing.json")]
        [InlineData("05-endOfConversation.json")]
        [InlineData("06-event.json")]
        public void ValidateActivity_FromJsonFile(string filename)
        {
            // Arrange
            var messageJsonPath = Path.Combine("Tests", filename);

            // Verify file exists
            Assert.True(File.Exists(messageJsonPath), $"Test file not found at: {messageJsonPath}");

            var messageJsonContent = File.ReadAllText(messageJsonPath);

            // Act
            var result = _validator.ValidateActivity(messageJsonContent);

            // Assert
            Assert.True(result.IsValid, $"Validation failed. Errors: {string.Join(", ", result.Errors)}");
            Assert.Equal("message", result.ActivityType);
            Assert.Empty(result.Errors);
            Assert.Contains("Activity validation successful", result.ValidationMessage);
        }

        [Fact]
        public async Task ValidateActivityAsync_FromMessageJsonFile_ShouldReturnValid()
        {
            // Arrange
            var messageJsonPath = Path.Combine("Tests", "message.json");

            // Verify file exists
            Assert.True(File.Exists(messageJsonPath), $"Test file not found at: {messageJsonPath}");

            var messageJsonContent = await File.ReadAllTextAsync(messageJsonPath);

            // Act
            var result = await _validator.ValidateActivityAsync(messageJsonContent);

            // Assert
            Assert.True(result.IsValid, $"Async validation failed. Errors: {string.Join(", ", result.Errors)}");
            Assert.Equal("message", result.ActivityType);
            Assert.Empty(result.Errors);
            Assert.Contains("Activity validation successful", result.ValidationMessage);
        }
    }
}