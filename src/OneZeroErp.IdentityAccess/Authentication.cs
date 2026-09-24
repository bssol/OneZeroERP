using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OneZeroErp.Application;

namespace OneZeroErp.IdentityAccess;

public sealed record AppUser(Guid Id, string UserName, string DisplayName, string PasswordHash, string Role, bool IsActive = true);

public interface IAppUserStore
{
    AppUser? FindByUserName(string userName);
}

public sealed class InMemoryAppUserStore(IConfiguration configuration) : IAppUserStore
{
    private readonly AppUser? _developmentUser = CreateDevelopmentUser(configuration);

    public AppUser? FindByUserName(string userName) =>
        _developmentUser is not null && string.Equals(_developmentUser.UserName, userName, StringComparison.OrdinalIgnoreCase)
            ? _developmentUser
            : null;

    private static AppUser? CreateDevelopmentUser(IConfiguration configuration)
    {
        var password = configuration["DevelopmentSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
            password = configuration["DevelopmentSeed__Password"];
        if (string.IsNullOrWhiteSpace(password))
            password = configuration["DevelopmentSeed_Password"];
        if (string.IsNullOrWhiteSpace(password)) return null;
        return new(Guid.NewGuid(), "admin", "Development Administrator", PasswordHasher.Hash(password), "Administrator");
    }
}

public static class PasswordHasher
{
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string CreateToken(AppUser user)
    {
        var key = configuration["Authentication:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Authentication:SigningKey must be configured with at least 32 characters.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class AppUserAuthenticationService(IAppUserStore users, JwtTokenService tokens) : IAuthenticationService
{
    public Task<AuthenticationResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = users.FindByUserName(request.UserName);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Task.FromResult(AuthenticationResult.Failure("Invalid credentials."));

        var identity = new ClaimsIdentity("OneZeroErpCookie", ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new(ClaimTypes.Name, user.DisplayName));
        identity.AddClaim(new(ClaimTypes.Role, user.Role));
        return Task.FromResult(new AuthenticationResult(true, tokens.CreateToken(user), new ClaimsPrincipal(identity), null));
    }
}
