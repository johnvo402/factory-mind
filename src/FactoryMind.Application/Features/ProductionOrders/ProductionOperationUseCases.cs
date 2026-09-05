using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders;

public sealed record GetProductionOrderOperationsQuery(Guid ProductionOrderId)
    : IRequest<Result<IReadOnlyList<ProductionOrderOperationResponse>>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record StartProductionOrderOperationCommand(Guid ProductionOrderId, Guid OperationId)
    : IRequest<Result<ProductionOrderOperationResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed record CompleteProductionOrderOperationCommand(Guid ProductionOrderId, Guid OperationId)
    : IRequest<Result<ProductionOrderOperationResponse>>, IAuthorizedRequest {
    public string Policy => AuthorizationPolicies.Manager;
}

public sealed class GetProductionOrderOperationsQueryHandler(
    IProductionExecutionRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<
        GetProductionOrderOperationsQuery,
        Result<IReadOnlyList<ProductionOrderOperationResponse>>> {
    public async ValueTask<Result<IReadOnlyList<ProductionOrderOperationResponse>>> Handle(
        GetProductionOrderOperationsQuery query,
        CancellationToken cancellationToken) {
        if (await repository.GetAsync(
                query.ProductionOrderId, currentUser.CompanyId, cancellationToken) is null) {
            return Result<IReadOnlyList<ProductionOrderOperationResponse>>.Failure(
                ProductionOrderErrors.NotFound);
        }
        var operations = await repository.GetOperationsAsync(
            query.ProductionOrderId, currentUser.CompanyId, cancellationToken);
        return Result<IReadOnlyList<ProductionOrderOperationResponse>>.Success(
            operations.Select(ProductionOrderOperationResponse.From).ToList());
    }
}

public sealed class StartProductionOrderOperationCommandHandler(
    IProductionExecutionRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<StartProductionOrderOperationCommand, Result<ProductionOrderOperationResponse>> {
    public async ValueTask<Result<ProductionOrderOperationResponse>> Handle(
        StartProductionOrderOperationCommand command,
        CancellationToken cancellationToken) {
        var order = await repository.GetAsync(
            command.ProductionOrderId, currentUser.CompanyId, cancellationToken);
        if (order is null) {
            return Result<ProductionOrderOperationResponse>.Failure(ProductionOrderErrors.NotFound);
        }
        if (order.Operations.All(operation => operation.Id != command.OperationId)) {
            return Result<ProductionOrderOperationResponse>.Failure(ProductionOrderErrors.OperationNotFound);
        }

        var result = await repository.TryStartOperationAsync(
            command.ProductionOrderId,
            command.OperationId,
            currentUser.CompanyId,
            DateTime.UtcNow,
            cancellationToken);
        return result.Status == ProductionExecutionStatus.Success
            ? Result<ProductionOrderOperationResponse>.Success(
                ProductionOrderOperationResponse.From(result.Operation!))
            : Result<ProductionOrderOperationResponse>.Failure(
                ProductionOrderErrors.OperationInvalidTransition);
    }
}

public sealed class CompleteProductionOrderOperationCommandHandler(
    IProductionExecutionRepository repository,
    ICurrentUser currentUser)
    : IRequestHandler<CompleteProductionOrderOperationCommand, Result<ProductionOrderOperationResponse>> {
    public async ValueTask<Result<ProductionOrderOperationResponse>> Handle(
        CompleteProductionOrderOperationCommand command,
        CancellationToken cancellationToken) {
        var order = await repository.GetAsync(
            command.ProductionOrderId, currentUser.CompanyId, cancellationToken);
        if (order is null) {
            return Result<ProductionOrderOperationResponse>.Failure(ProductionOrderErrors.NotFound);
        }
        if (order.Operations.All(operation => operation.Id != command.OperationId)) {
            return Result<ProductionOrderOperationResponse>.Failure(ProductionOrderErrors.OperationNotFound);
        }

        var result = await repository.TryCompleteOperationAsync(
            command.ProductionOrderId,
            command.OperationId,
            currentUser.CompanyId,
            DateTime.UtcNow,
            cancellationToken);
        return result.Status == ProductionExecutionStatus.Success
            ? Result<ProductionOrderOperationResponse>.Success(
                ProductionOrderOperationResponse.From(result.Operation!))
            : Result<ProductionOrderOperationResponse>.Failure(
                ProductionOrderErrors.OperationInvalidTransition);
    }
}
