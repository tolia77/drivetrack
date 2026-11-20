using System.Security.Claims;
using System.Text;
using DriveTrack.Application.Abstractions;
using DriveTrack.Application.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DriveTrack.Infrastructure.Identity;

/// <summary>
/// Signs the bearer token the REST adapter accepts.
/// <para>
/// The claims come from <see cref="SessionClaims"/>, which the cookie scheme writes too: a caller is
/// the same <c>ICurrentUser</c> whichever credential carried them.
/// </para>
/// <para>
/// The expiry is read from the injected <see cref="TimeProvider"/>, never from the ambient
/// clock, so a token-lifetime rule can be tested by moving a fake clock rather than by waiting.
/// </para>
/// </summary>
internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public string Issue(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var issuedAt = timeProvider.GetUtcNow();

        // The same list the cookie is written with, so the two schemes cannot drift apart.
        var claims = SessionClaims.For(account).Select(claim => new Claim(claim.Type, claim.Value));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.AddMinutes(_options.LifetimeMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }
}
