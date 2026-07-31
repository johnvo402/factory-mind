using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Knowledge.ProcessDocument;

public sealed record ProcessDocumentCommand(
    Guid DocumentId,
    Guid CompanyId) : IRequest<Result>;
