using FluentValidation;

namespace FactoryMind.Application.Features.Chat.CreateConversation;

public sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand> {
    public CreateConversationCommandValidator() {
        RuleFor(command => command.Title)
            .MaximumLength(120).WithMessage("Conversation title must not exceed 120 characters.");
    }
}
