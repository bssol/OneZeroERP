using Microsoft.EntityFrameworkCore;
using OneZeroErp.IdentityAccess;

namespace OneZeroErp.Infrastructure.Persistence;

public sealed class SqlAppUserStore(IUnitOfWork unitOfWork) : IAppUserStore
{
    public async Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var normalizedUserName = userName.Trim();
        var entity = await unitOfWork.AppUsers.QueryNonTracking
            .Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.UserName == normalizedUserName, cancellationToken);

        return entity is null
            ? null
            : new AppUser(
                entity.Id,
                entity.UserName,
                entity.DisplayName,
                entity.PasswordHash,
                entity.Role,
                entity.IsActive,
                entity.Permissions.Select(x => x.Permission).ToArray());
    }
}
