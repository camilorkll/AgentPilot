using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

public class UserRepository(AgentPilotDbContext db) : IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => db.Users.AnyAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await db.Users.AddAsync(user, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
