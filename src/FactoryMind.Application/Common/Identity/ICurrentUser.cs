namespace FactoryMind.Application.Common.Identity;

public interface ICurrentUser {
    Guid UserId { get; }
    Guid CompanyId { get; }
    string Role { get; }
}
