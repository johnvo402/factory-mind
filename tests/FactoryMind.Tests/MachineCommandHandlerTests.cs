using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Machines.CreateMachine;
using FactoryMind.Application.Features.Machines.DeleteMachine;
using FactoryMind.Application.Features.Machines.GetMachines;
using FactoryMind.Application.Features.Machines.UpdateMachine;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class MachineCommandHandlerTests {
    [Fact]
    public async Task Create_normalizes_machine_and_uses_current_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMachineRepository();
        var handler = new CreateMachineCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new CreateMachineCommand("  m-002 ", "  Packing line  ", " RUNNING "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var machine = Assert.Single(repository.Machines);
        Assert.Equal(currentUser.CompanyId, machine.CompanyId);
        Assert.Equal("M-002", machine.Code);
        Assert.Equal("Packing line", machine.Name);
        Assert.Equal(MachineStatuses.Running, machine.Status);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code_in_the_same_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMachineRepository();
        repository.Machines.Add(new Machine {
            CompanyId = currentUser.CompanyId,
            Code = "M-001",
            Name = "Existing machine"
        });
        var handler = new CreateMachineCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new CreateMachineCommand("m-001", "Duplicate", MachineStatuses.Available),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("machines.code_already_exists", result.Error?.Code);
        Assert.Single(repository.Machines);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task List_uses_company_scope_and_normalized_search() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMachineRepository();
        repository.Machines.Add(new Machine {
            CompanyId = currentUser.CompanyId,
            Code = "M-001",
            Name = "Injection molding"
        });
        var handler = new GetMachinesQueryHandler(repository, currentUser);

        var result = await handler.Handle(new GetMachinesQuery("  injection "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(currentUser.CompanyId, repository.RequestedCompanyId);
        Assert.Equal("injection", repository.RequestedSearch);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task Update_cannot_access_a_machine_from_another_company() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMachineRepository();
        repository.Machines.Add(new Machine {
            CompanyId = Guid.NewGuid(),
            Code = "M-001",
            Name = "Other tenant machine"
        });
        var handler = new UpdateMachineCommandHandler(repository, currentUser);

        var result = await handler.Handle(
            new UpdateMachineCommand(
                repository.Machines[0].Id,
                "M-001",
                "Changed",
                MachineStatuses.Offline),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("machines.not_found", result.Error?.Code);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Delete_removes_a_company_machine() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeMachineRepository();
        var machine = new Machine {
            CompanyId = currentUser.CompanyId,
            Code = "M-001",
            Name = "Injection molding"
        };
        repository.Machines.Add(machine);
        var handler = new DeleteMachineCommandHandler(repository, currentUser);

        var result = await handler.Handle(new DeleteMachineCommand(machine.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Machines);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Admin";
    }

    private sealed class FakeMachineRepository : IMachineRepository {
        public List<Machine> Machines { get; } = [];
        public Guid? RequestedCompanyId { get; private set; }
        public string? RequestedSearch { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<Machine>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) {
            RequestedCompanyId = companyId;
            RequestedSearch = search;
            IReadOnlyList<Machine> machines = Machines
                .Where(machine => machine.CompanyId == companyId)
                .ToList();
            return Task.FromResult(machines);
        }

        public Task<Machine?> GetByIdAsync(
            Guid machineId,
            Guid companyId,
            CancellationToken cancellationToken) {
            RequestedCompanyId = companyId;
            return Task.FromResult(Machines.SingleOrDefault(machine =>
                machine.Id == machineId && machine.CompanyId == companyId));
        }

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedMachineId,
            CancellationToken cancellationToken) {
            var exists = Machines.Any(machine =>
                machine.CompanyId == companyId &&
                machine.Code == code &&
                (!excludedMachineId.HasValue || machine.Id != excludedMachineId.Value));
            return Task.FromResult(exists);
        }

        public void Add(Machine machine) => Machines.Add(machine);
        public void Remove(Machine machine) => Machines.Remove(machine);

        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
