using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.BusinessData;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.Machines.GetMachines;

public sealed class GetMachinesQueryHandler(
    IMachineRepository repository,
    ICurrentUser currentUser) : IRequestHandler<GetMachinesQuery, Result<IReadOnlyList<MachineResponse>>> {
    public async ValueTask<Result<IReadOnlyList<MachineResponse>>> Handle(
        GetMachinesQuery query,
        CancellationToken cancellationToken) {
        var machines = await repository.GetByCompanyAsync(
            currentUser.CompanyId,
            BusinessDataNormalization.Search(query.Search),
            cancellationToken);
        var response = machines.Select(MachineResponse.From).ToList();
        return Result<IReadOnlyList<MachineResponse>>.Success(response);
    }
}
