using System.Security.Cryptography;
using System.Text;
using AlfajorgeBackend.Data;
using AlfajorgeBackend.Models;
using AlfajorgeBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace Alfajorge.Api.Tests;

public class PasswordHashTests
{
    [Fact]
    public void NewPasswordsUseAOneWayIdentityPasswordHash()
    {
        const string password = "correct horse battery staple";

        var hash = AuthService.HashPassword(password);

        Assert.NotEqual(password, hash);
        Assert.NotEqual(64, hash.Length);
        Assert.True(AuthService.VerifyPassword(password, hash));
        Assert.False(AuthService.VerifyPassword("incorrect password", hash));
    }

    [Fact]
    public void LegacySha256HashesRemainVerifiableForLoginUpgrade()
    {
        const string legacyPassword = "Admin123!";
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacyPassword)));

        Assert.True(AuthService.VerifyPassword(legacyPassword, legacyHash));
        Assert.False(AuthService.VerifyPassword("wrong password", legacyHash));
    }

    [Fact]
    public void GeneratedTokenIsAStandardIssuerAndAudienceValidatedJwt()
    {
        const string key = "unit-test-signing-key-with-more-than-32-bytes";
        const string issuer = "Alfajorge.Tests";
        const string audience = "Alfajorge.Web.Tests";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ALFAJORGE_TOKEN_SECRET"] = key,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience
            })
            .Build();
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options);
        var auth = new AuthService(context, configuration);
        var persona = new Persona { IdPersona = 42, Nombre = "Test", Apellido = "User", Email = "test@example.invalid" };

        var jwt = auth.GenerateToken(persona, "VENDEDOR");
        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _);

        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("VENDEDOR", principal.FindFirstValue(ClaimTypes.Role));
    }
}
