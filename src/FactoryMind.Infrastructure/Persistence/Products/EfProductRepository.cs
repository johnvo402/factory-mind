using FactoryMind.Application.Features.Products;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Products;

public sealed class EfProductRepository(FactoryMindDbContext dbContext) : IProductRepository {
    public async Task<IReadOnlyList<Product>> GetByCompanyAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken) {
        var query = dbContext.Products
            .AsNoTracking()
            .Where(product => product.CompanyId == companyId);
        if (search is not null) {
            var pattern = $"%{search}%";
            query = query.Where(product =>
                EF.Functions.ILike(product.Code, pattern) ||
                EF.Functions.ILike(product.Name, pattern));
        }

        return await query.OrderBy(product => product.Code).ToListAsync(cancellationToken);
    }

    public Task<Product?> GetByIdAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == productId && product.CompanyId == companyId,
            cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludedProductId,
        CancellationToken cancellationToken) => dbContext.Products.AnyAsync(
            product => product.CompanyId == companyId &&
                product.Code == code &&
                (!excludedProductId.HasValue || product.Id != excludedProductId.Value),
            cancellationToken);

    public Task<bool> HasBillOfMaterialsAsync(
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken) => dbContext.BillOfMaterials.AnyAsync(
            bom => bom.ProductId == productId && bom.CompanyId == companyId,
            cancellationToken);

    public void Add(Product product) => dbContext.Products.Add(product);
    public void Remove(Product product) => dbContext.Products.Remove(product);
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
