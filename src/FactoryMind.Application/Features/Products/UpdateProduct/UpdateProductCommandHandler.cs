using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.UpdateProduct;

public sealed class UpdateProductCommandHandler(
    IProductRepository repository,
    ICurrentUser currentUser) : IRequestHandler<UpdateProductCommand, Result<ProductResponse>> {
    public async ValueTask<Result<ProductResponse>> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken) {
        var product = await repository.GetByIdAsync(
            command.ProductId,
            currentUser.CompanyId,
            cancellationToken);
        if (product is null) {
            return Result<ProductResponse>.Failure(ProductErrors.NotFound);
        }

        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(
                currentUser.CompanyId,
                code,
                product.Id,
                cancellationToken)) {
            return Result<ProductResponse>.Failure(ProductErrors.CodeAlreadyExists);
        }

        product.Code = code;
        product.Name = BusinessDataNormalization.Name(command.Name);
        product.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<ProductResponse>.Success(ProductResponse.From(product));
    }
}
