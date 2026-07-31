using FluentValidation;

namespace FactoryMind.Application.Features.Chat.SendMessage;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest> {
    public SendMessageRequestValidator() {
        RuleFor(request => request.Content)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(8_000).WithMessage("Message must not exceed 8000 characters.");
    }
}
