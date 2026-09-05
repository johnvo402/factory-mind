using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Routings;

public sealed record GetRoutingsQuery(Guid ProductId)
    : IRequest<Result<IReadOnlyList<RoutingResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record GetRoutingQuery(Guid ProductId, Guid RoutingId)
    : IRequest<Result<RoutingResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record CreateRoutingCommand(
    Guid ProductId,
    IReadOnlyList<RoutingOperationDefinition> Operations)
    : IRequest<Result<RoutingResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record UpdateRoutingCommand(
    Guid ProductId,
    Guid RoutingId,
    IReadOnlyList<RoutingOperationDefinition> Operations)
    : IRequest<Result<RoutingResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record ActivateRoutingCommand(Guid ProductId, Guid RoutingId)
    : IRequest<Result<RoutingResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed class GetRoutingsQueryHandler(
    IRoutingRepository repository,
    IProductRepository productRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetRoutingsQuery, Result<IReadOnlyList<RoutingResponse>>> {
    public async ValueTask<Result<IReadOnlyList<RoutingResponse>>> Handle(
        GetRoutingsQuery query,
        CancellationToken cancellationToken) {
        if (await productRepository.GetByIdAsync(
                query.ProductId, currentUser.CompanyId, cancellationToken) is null) {
            return Result<IReadOnlyList<RoutingResponse>>.Failure(ProductErrors.NotFound);
        }
        var routings = await repository.GetByProductAsync(
            query.ProductId, currentUser.CompanyId, cancellationToken);
        return Result<IReadOnlyList<RoutingResponse>>.Success(
            routings.Select(RoutingResponse.From).ToList());
    }
}

public sealed class GetRoutingQueryHandler(IRoutingRepository repository, ICurrentUser currentUser)
    : IRequestHandler<GetRoutingQuery, Result<RoutingResponse>> {
    public async ValueTask<Result<RoutingResponse>> Handle(
        GetRoutingQuery query,
        CancellationToken cancellationToken) {
        var routing = await repository.GetByIdAsync(
            query.RoutingId, query.ProductId, currentUser.CompanyId, cancellationToken);
        return routing is null
            ? Result<RoutingResponse>.Failure(RoutingErrors.NotFound)
            : Result<RoutingResponse>.Success(RoutingResponse.From(routing));
    }
}

public sealed class CreateRoutingCommandHandler(
    IRoutingRepository repository,
    IProductRepository productRepository,
    IWorkCenterRepository workCenterRepository,
    ICurrentUser currentUser)
    : IRequestHandler<CreateRoutingCommand, Result<RoutingResponse>> {
    public async ValueTask<Result<RoutingResponse>> Handle(
        CreateRoutingCommand command,
        CancellationToken cancellationToken) {
        var product = await productRepository.GetByIdAsync(
            command.ProductId, currentUser.CompanyId, cancellationToken);
        if (product is null) {
            return Result<RoutingResponse>.Failure(ProductErrors.NotFound);
        }
        var validation = RoutingSpecificationValidation.Validate(command.Operations);
        if (validation is not null) {
            return Result<RoutingResponse>.Failure(validation);
        }
        var workCenters = await ResolveWorkCentersAsync(
            command.Operations, workCenterRepository, currentUser.CompanyId, cancellationToken);
        if (workCenters is null) {
            return Result<RoutingResponse>.Failure(RoutingErrors.WorkCenterNotFound);
        }

        var now = DateTime.UtcNow;
        var routing = new Routing {
            CompanyId = currentUser.CompanyId,
            ProductId = product.Id,
            Product = product,
            Revision = await repository.GetNextRevisionAsync(
                product.Id, currentUser.CompanyId, cancellationToken),
            Status = RoutingStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        routing.Operations = MapOperations(routing, command.Operations, workCenters, now);
        repository.Add(routing);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<RoutingResponse>.Success(RoutingResponse.From(routing));
    }

    internal static async Task<Dictionary<Guid, WorkCenter>?> ResolveWorkCentersAsync(
        IReadOnlyList<RoutingOperationDefinition> operations,
        IWorkCenterRepository repository,
        Guid companyId,
        CancellationToken cancellationToken) {
        var workCenters = new Dictionary<Guid, WorkCenter>();
        foreach (var id in operations.Select(operation => operation.WorkCenterId).Distinct()) {
            var workCenter = await repository.GetByIdAsync(id, companyId, cancellationToken);
            if (workCenter is null) {
                return null;
            }
            workCenters.Add(id, workCenter);
        }
        return workCenters;
    }

    internal static List<RoutingOperation> MapOperations(
        Routing routing,
        IReadOnlyList<RoutingOperationDefinition> definitions,
        IReadOnlyDictionary<Guid, WorkCenter> workCenters,
        DateTime now) => definitions.Select(definition => new RoutingOperation {
            RoutingId = routing.Id,
            Routing = routing,
            Sequence = definition.Sequence,
            Name = definition.Name.Trim(),
            WorkCenterId = definition.WorkCenterId,
            WorkCenter = workCenters[definition.WorkCenterId],
            SetupTimeMinutes = definition.SetupTimeMinutes,
            RunTimeMinutes = definition.RunTimeMinutes,
            Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();
}

public sealed class UpdateRoutingCommandHandler(
    IRoutingRepository repository,
    IWorkCenterRepository workCenterRepository,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateRoutingCommand, Result<RoutingResponse>> {
    public async ValueTask<Result<RoutingResponse>> Handle(
        UpdateRoutingCommand command,
        CancellationToken cancellationToken) {
        var routing = await repository.GetByIdAsync(
            command.RoutingId, command.ProductId, currentUser.CompanyId, cancellationToken);
        if (routing is null) {
            return Result<RoutingResponse>.Failure(RoutingErrors.NotFound);
        }
        if (routing.Status != RoutingStatuses.Draft) {
            return Result<RoutingResponse>.Failure(RoutingErrors.DraftRequired);
        }
        var validation = RoutingSpecificationValidation.Validate(command.Operations);
        if (validation is not null) {
            return Result<RoutingResponse>.Failure(validation);
        }
        var workCenters = await CreateRoutingCommandHandler.ResolveWorkCentersAsync(
            command.Operations, workCenterRepository, currentUser.CompanyId, cancellationToken);
        if (workCenters is null) {
            return Result<RoutingResponse>.Failure(RoutingErrors.WorkCenterNotFound);
        }

        var now = DateTime.UtcNow;
        var operations = CreateRoutingCommandHandler.MapOperations(
            routing, command.Operations, workCenters, now);
        var updatedRouting = await repository.ReplaceDraftOperationsAsync(
            routing, operations, now, cancellationToken);
        return updatedRouting is null
            ? Result<RoutingResponse>.Failure(RoutingErrors.DraftRequired)
            : Result<RoutingResponse>.Success(RoutingResponse.From(updatedRouting));
    }
}

public sealed class ActivateRoutingCommandHandler(IRoutingRepository repository, ICurrentUser currentUser)
    : IRequestHandler<ActivateRoutingCommand, Result<RoutingResponse>> {
    public async ValueTask<Result<RoutingResponse>> Handle(
        ActivateRoutingCommand command,
        CancellationToken cancellationToken) {
        var result = await repository.TryActivateAsync(
            command.RoutingId,
            command.ProductId,
            currentUser.CompanyId,
            DateTime.UtcNow,
            cancellationToken);
        return result.Status switch {
            RoutingActivationStatus.Success => Result<RoutingResponse>.Success(
                RoutingResponse.From(result.Routing!)),
            RoutingActivationStatus.NotFound => Result<RoutingResponse>.Failure(RoutingErrors.NotFound),
            RoutingActivationStatus.OperationsRequired =>
                Result<RoutingResponse>.Failure(RoutingErrors.OperationsRequired),
            RoutingActivationStatus.InvalidSequence =>
                Result<RoutingResponse>.Failure(RoutingErrors.SequenceInvalid),
            RoutingActivationStatus.InvalidTiming =>
                Result<RoutingResponse>.Failure(RoutingErrors.TimingInvalid),
            RoutingActivationStatus.WorkCenterUnavailable =>
                Result<RoutingResponse>.Failure(RoutingErrors.WorkCenterInactive),
            _ => Result<RoutingResponse>.Failure(RoutingErrors.DraftRequired)
        };
    }
}
