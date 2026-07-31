using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.GetAiSettings;

public sealed class GetAiSettingsQueryHandler(
    IAiSettingsReader reader) : IRequestHandler<GetAiSettingsQuery, Result<AiSettingsResponse>> {
    public ValueTask<Result<AiSettingsResponse>> Handle(
        GetAiSettingsQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result<AiSettingsResponse>.Success(reader.Get()));
}
