using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.WorkCenters;

public sealed record GetWorkCentersQuery(string? Search)
    : IRequest<Result<IReadOnlyList<WorkCenterResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record GetWorkCenterQuery(Guid WorkCenterId)
    : IRequest<Result<WorkCenterResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record CreateWorkCenterCommand(string Code, string Name, string? Description)
    : IRequest<Result<WorkCenterResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record UpdateWorkCenterCommand(
    Guid WorkCenterId,
    string Code,
    string Name,
    string? Description)
    : IRequest<Result<WorkCenterResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record DeactivateWorkCenterCommand(Guid WorkCenterId)
    : IRequest<Result<WorkCenterResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed class GetWorkCentersQueryHandler(IWorkCenterRepository repository, ICurrentUser currentUser)
    : IRequestHandler<GetWorkCentersQuery, Result<IReadOnlyList<WorkCenterResponse>>> {
    public async ValueTask<Result<IReadOnlyList<WorkCenterResponse>>> Handle(
        GetWorkCentersQuery query,
        CancellationToken cancellationToken) {
        var workCenters = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        return Result<IReadOnlyList<WorkCenterResponse>>.Success(
            workCenters.Select(WorkCenterResponse.From).ToList());
    }
}

public sealed class GetWorkCenterQueryHandler(IWorkCenterRepository repository, ICurrentUser currentUser)
    : IRequestHandler<GetWorkCenterQuery, Result<WorkCenterResponse>> {
    public async ValueTask<Result<WorkCenterResponse>> Handle(
        GetWorkCenterQuery query,
        CancellationToken cancellationToken) {
        var workCenter = await repository.GetByIdAsync(
            query.WorkCenterId, currentUser.CompanyId, cancellationToken);
        return workCenter is null
            ? Result<WorkCenterResponse>.Failure(WorkCenterErrors.NotFound)
            : Result<WorkCenterResponse>.Success(WorkCenterResponse.From(workCenter));
    }
}

public sealed class CreateWorkCenterCommandHandler(IWorkCenterRepository repository, ICurrentUser currentUser)
    : IRequestHandler<CreateWorkCenterCommand, Result<WorkCenterResponse>> {
    public async ValueTask<Result<WorkCenterResponse>> Handle(
        CreateWorkCenterCommand command,
        CancellationToken cancellationToken) {
        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(currentUser.CompanyId, code, null, cancellationToken)) {
            return Result<WorkCenterResponse>.Failure(WorkCenterErrors.CodeAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var workCenter = new WorkCenter {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = BusinessDataNormalization.Name(command.Name),
            Description = NormalizeOptional(command.Description),
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.Add(workCenter);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<WorkCenterResponse>.Success(WorkCenterResponse.From(workCenter));
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim();
}

public sealed class UpdateWorkCenterCommandHandler(IWorkCenterRepository repository, ICurrentUser currentUser)
    : IRequestHandler<UpdateWorkCenterCommand, Result<WorkCenterResponse>> {
    public async ValueTask<Result<WorkCenterResponse>> Handle(
        UpdateWorkCenterCommand command,
        CancellationToken cancellationToken) {
        var workCenter = await repository.GetByIdAsync(
            command.WorkCenterId, currentUser.CompanyId, cancellationToken);
        if (workCenter is null) {
            return Result<WorkCenterResponse>.Failure(WorkCenterErrors.NotFound);
        }

        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(
                currentUser.CompanyId, code, workCenter.Id, cancellationToken)) {
            return Result<WorkCenterResponse>.Failure(WorkCenterErrors.CodeAlreadyExists);
        }

        workCenter.Code = code;
        workCenter.Name = BusinessDataNormalization.Name(command.Name);
        workCenter.Description = NormalizeOptional(command.Description);
        workCenter.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<WorkCenterResponse>.Success(WorkCenterResponse.From(workCenter));
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim();
}

public sealed class DeactivateWorkCenterCommandHandler(IWorkCenterRepository repository, ICurrentUser currentUser)
    : IRequestHandler<DeactivateWorkCenterCommand, Result<WorkCenterResponse>> {
    public async ValueTask<Result<WorkCenterResponse>> Handle(
        DeactivateWorkCenterCommand command,
        CancellationToken cancellationToken) {
        var workCenter = await repository.GetByIdAsync(
            command.WorkCenterId, currentUser.CompanyId, cancellationToken);
        if (workCenter is null) {
            return Result<WorkCenterResponse>.Failure(WorkCenterErrors.NotFound);
        }

        workCenter.IsActive = false;
        workCenter.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<WorkCenterResponse>.Success(WorkCenterResponse.From(workCenter));
    }
}
