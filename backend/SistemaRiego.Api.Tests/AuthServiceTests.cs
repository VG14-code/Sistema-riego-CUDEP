using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;
namespace SistemaRiego.Api.Tests;
public sealed class AuthServiceTests
{
    [Fact] public async Task Register_AssignsOperatorRole_AndHashesPassword()
    {
        var (service, db) = Create(); var user = await service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona de prueba"), default);
        Assert.Contains(RoleNames.Operator, user.Roles); var stored = await db.Users.SingleAsync(); Assert.NotEqual("Segura123!", stored.PasswordHash);
    }
    [Fact] public async Task Login_ReturnsJwtAndRefreshToken_ForValidCredentials()
    {
        var (service, _) = Create(); await service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var response = await service.LoginAsync(new("persona@correo.gt", "Segura123!"), new("127.0.0.1", "tests"), default);
        Assert.NotNull(response); Assert.Contains('.', response!.AccessToken); Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }
    [Fact] public async Task Login_RejectsInvalidPassword_AndAuditsAttempt()
    {
        var (service, db) = Create(); await service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        Assert.Null(await service.LoginAsync(new("persona@correo.gt", "incorrecta"), new("127.0.0.1", "tests"), default));
        Assert.Equal(1, await db.AccessAudits.CountAsync(x => x.EventType == "LOGIN_FAILED"));
    }
    [Fact] public async Task Refresh_RotatesToken_AndRevokesPreviousSession()
    {
        var (service, db) = Create(); await service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var login = await service.LoginAsync(new("persona@correo.gt", "Segura123!"), new(null, null), default);
        var refreshed = await service.RefreshAsync(login!.RefreshToken, new(null, null), default);
        Assert.NotNull(refreshed); Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken); Assert.Equal(1, await db.Sessions.CountAsync(x => x.RevokedAtUtc != null));
    }
    [Fact] public async Task Logout_RevokesRefreshToken()
    {
        var (service, _) = Create(); await service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var login = await service.LoginAsync(new("persona@correo.gt", "Segura123!"), new(null, null), default);
        Assert.True(await service.LogoutAsync(login!.RefreshToken, default)); Assert.False(await service.LogoutAsync(login.RefreshToken, default));
    }
    [Fact] public async Task PasswordReset_ChangesPassword_AndInvalidatesToken()
    {
        var (service, _) = Create(); await service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var token = await service.RequestPasswordRecoveryAsync("persona@correo.gt", default);
        Assert.True(await service.ResetPasswordAsync(new(token!, "Nueva123!"), default)); Assert.False(await service.ResetPasswordAsync(new(token!, "Otra123!"), default));
        Assert.NotNull(await service.LoginAsync(new("persona@correo.gt", "Nueva123!"), new(null, null), default));
    }
    private static (AuthService Service, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(
            new Role { Name = RoleNames.Administrator, NormalizedName = RoleNames.Administrator.ToUpperInvariant(), Description = "Admin" },
            new Role { Name = RoleNames.Technician, NormalizedName = RoleNames.Technician.ToUpperInvariant(), Description = "Técnico" },
            new Role { Name = RoleNames.Operator, NormalizedName = RoleNames.Operator.ToUpperInvariant(), Description = "Operador" });
        db.SaveChanges();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection(); services.AddSingleton(db);
        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions { Issuer = "tests", Audience = "tests", SigningKey = "clave-de-pruebas-con-mas-de-32-caracteres-segura", AccessTokenMinutes = 15, RefreshTokenDays = 7, PasswordRecoveryMinutes = 30 }));
        services.AddIdentityCore<User>(options => { options.Password.RequiredLength = 8; options.Password.RequireUppercase = true; options.Password.RequireLowercase = true; options.Password.RequireDigit = true; options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5; }).AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        services.AddScoped<AuthService>();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<AuthService>(), db);
    }
}
