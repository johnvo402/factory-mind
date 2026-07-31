using FactoryMind.Application.Common.Identity;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.GetDocuments;

public sealed class GetDocumentsQueryHandler(
    IDocumentRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetDocumentsQuery, Result<IReadOnlyList<DocumentResponse>>> {
    public async ValueTask<Result<IReadOnlyList<DocumentResponse>>> Handle(
        GetDocumentsQuery query,
        CancellationToken cancellationToken) {
        var documents = await repository.GetByCompanyAsync(currentUser.CompanyId, cancellationToken);
        var response = documents
            .Select(DocumentResponse.From)
            .ToList();
        return Result<IReadOnlyList<DocumentResponse>>.Success(response);
    }
}
