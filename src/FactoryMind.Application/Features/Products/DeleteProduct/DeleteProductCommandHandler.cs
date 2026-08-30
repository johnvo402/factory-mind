using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.DeleteProduct;

public sealed class DeleteProductCommandHandler(
    IProductRepository repository,
    ICurrentUser currentUser) : IRequestHandler<DeleteProductCommand, Result> {
    public async ValueTask<Result> Handle(
        DeleteProductCommand command,
        CancellationToken cancellationToken) {
        var product = await repository.GetByIdAsync(
            command.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result.Failure(ProductErrors.NotFound);
        }

        if (await repository.HasReferencesAsync(
                product.Id,
                currentUser.CompanyId,
                cancellationToken)) {
            return Result.Failure(ProductErrors.Referenced);
        }

        repository.Remove(product);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
