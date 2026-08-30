using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.Application.Features.Products;

public static class ProductConstraints {
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 200;
    public const int MaximumSearchLength = 200;
}

public sealed record ProductResponse(
    Guid Id,
    string Code,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt) {
    public static ProductResponse From(Product product) => new(
        product.Id,
        product.Code,
        product.Name,
        product.CreatedAt,
        product.UpdatedAt);
}

public interface IProductRepository {
    Task<IReadOnlyList<Product>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedProductId,
        CancellationToken cancellationToken);

    Task<bool> HasReferencesAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken);

    void Add(Product product);
    void Remove(Product product);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public static class ProductErrors {
    public static readonly Error NotFound = new(
        "products.not_found",
        "Product was not found.",
        404);

    public static readonly Error CodeAlreadyExists = new(
        "products.code_already_exists",
        "A product with this code already exists.",
        409);

    public static readonly Error Referenced = new(
        "products.referenced",
        "A product referenced by manufacturing or finished-goods records cannot be deleted.",
        409);
}
