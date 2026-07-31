using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Products.CreateProduct;

public sealed class CreateProductCommandHandler(
    IProductRepository repository,
    ICurrentUser currentUser) : IRequestHandler<CreateProductCommand, Result<ProductResponse>> {
    public async ValueTask<Result<ProductResponse>> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken) {
        var code = BusinessDataNormalization.Code(command.Code);
        if (await repository.CodeExistsAsync(currentUser.CompanyId, code, null, cancellationToken)) {
            return Result<ProductResponse>.Failure(ProductErrors.CodeAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var product = new Product {
            CompanyId = currentUser.CompanyId,
            Code = code,
            Name = BusinessDataNormalization.Name(command.Name),
            CreatedAt = now,
            UpdatedAt = now
        };
        repository.Add(product);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<ProductResponse>.Success(ProductResponse.From(product));
    }
}
