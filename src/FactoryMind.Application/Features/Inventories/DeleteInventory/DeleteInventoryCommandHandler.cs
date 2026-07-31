using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Inventories.DeleteInventory;

public sealed class DeleteInventoryCommandHandler(
    IInventoryRepository repository,
    ICurrentUser currentUser) : IRequestHandler<DeleteInventoryCommand, Result> {
    public async ValueTask<Result> Handle(
        DeleteInventoryCommand command,
        CancellationToken cancellationToken) {
        var inventory = await repository.GetByIdAsync(
            command.InventoryId,
            currentUser.CompanyId,
            cancellationToken);
        if (inventory is null) {
            return Result.Failure(InventoryErrors.NotFound);
        }

        repository.Remove(inventory);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
