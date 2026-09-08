using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Settings.UpdateCompanySettings;

public sealed class UpdateCompanySettingsCommandHandler(
    ISettingsRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateCompanySettingsCommand, Result<CompanySettingsResponse>> {
    public async ValueTask<Result<CompanySettingsResponse>> Handle(
        UpdateCompanySettingsCommand command,
        CancellationToken cancellationToken) {
        var company = await repository.GetCompanyAsync(currentUser.CompanyId, cancellationToken);
        if (company is null) {
            return Result<CompanySettingsResponse>.Failure(SettingsErrors.CompanyNotFound);
        }

        var timeZoneId = command.TimeZoneId.Trim();
        try {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        } catch (TimeZoneNotFoundException) {
            return Result<CompanySettingsResponse>.Failure(SettingsErrors.InvalidTimeZone);
        } catch (InvalidTimeZoneException) {
            return Result<CompanySettingsResponse>.Failure(SettingsErrors.InvalidTimeZone);
        }

        company.Name = command.Name.Trim();
        company.TimeZoneId = timeZoneId;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<CompanySettingsResponse>.Success(CompanySettingsResponse.From(company));
    }
}
