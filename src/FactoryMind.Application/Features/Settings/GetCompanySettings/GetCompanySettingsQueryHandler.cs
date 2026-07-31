using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.GetCompanySettings;

public sealed class GetCompanySettingsQueryHandler(
    ISettingsRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetCompanySettingsQuery, Result<CompanySettingsResponse>> {
    public async ValueTask<Result<CompanySettingsResponse>> Handle(
        GetCompanySettingsQuery query,
        CancellationToken cancellationToken) {
        var company = await repository.GetCompanyAsync(currentUser.CompanyId, cancellationToken);
        return company is null
            ? Result<CompanySettingsResponse>.Failure(SettingsErrors.CompanyNotFound)
            : Result<CompanySettingsResponse>.Success(CompanySettingsResponse.From(company));
    }
}
