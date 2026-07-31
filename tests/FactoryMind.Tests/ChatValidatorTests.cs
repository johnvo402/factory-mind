using FactoryMind.Application.Features.Chat.CreateConversation;
using FactoryMind.Application.Features.Chat.SendMessage;
using FluentValidation.TestHelper;

namespace FactoryMind.Tests;

public sealed class ChatValidatorTests {
    [Fact]
    public async Task Send_message_requires_content() {
        var validator = new SendMessageRequestValidator();

        var result = await validator.TestValidateAsync(new SendMessageRequest(""));

        result.ShouldHaveValidationErrorFor(request => request.Content)
            .WithErrorMessage("Message is required.");
    }

    [Fact]
    public async Task Conversation_title_is_limited_to_120_characters() {
        var validator = new CreateConversationCommandValidator();

        var result = await validator.TestValidateAsync(new CreateConversationCommand(new string('x', 121)));

        result.ShouldHaveValidationErrorFor(command => command.Title)
            .WithErrorMessage("Conversation title must not exceed 120 characters.");
    }
}
