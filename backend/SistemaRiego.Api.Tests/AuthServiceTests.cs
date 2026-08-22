using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class AuthServiceTests
{
    private static readonly AuthContext TestContext = new("127.0.0.1", "tests");

    [Fact]
    public async Task Register_AssignsOperatorRole_AndHashesPassword()
    {
        var setup = Create();
        var user = await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona de prueba"), default);
        Assert.Contains(RoleNames.Operator, user.Roles);
        var stored = await setup.Db.Users.SingleAsync();
        Assert.NotEqual("Segura123!", stored.PasswordHash);
    }

    [Fact]
    public async Task Login_ReturnsJwtAndRefreshToken_ForValidCredentials()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var response = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);
        Assert.NotNull(response);
        Assert.Contains('.', response!.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task Login_RejectsInvalidPassword_AndAuditsAttempt()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        Assert.Null(await setup.Service.LoginAsync(new("persona@correo.gt", "incorrecta"), TestContext, default));
        Assert.Equal(1, await setup.Db.AccessAudits.CountAsync(x => x.EventType == "LOGIN_FAILED"));
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRevokesPreviousSession()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var login = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);
        var refreshed = await setup.Service.RefreshAsync(login!.RefreshToken, TestContext, default);
        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);
        Assert.Equal(1, await setup.Db.Sessions.CountAsync(x => x.RevokedAtUtc != null));
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var login = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);
        Assert.True(await setup.Service.LogoutAsync(login!.RefreshToken, default));
        Assert.False(await setup.Service.LogoutAsync(login.RefreshToken, default));
    }

    [Fact]
    public async Task PasswordReset_IsOneUse_ChangesPassword_AndInvalidatesSessions()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var oldSession = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);
        await setup.Service.RequestPasswordRecoveryAsync("persona@correo.gt", TestContext, default);
        var token = setup.Mail.LastToken;

        Assert.True(await setup.Service.ResetPasswordAsync(new(token, "Nueva123!"), TestContext, default));
        Assert.False(await setup.Service.ResetPasswordAsync(new(token, "Otra123!"), TestContext, default));
        Assert.Null(await setup.Service.RefreshAsync(oldSession!.RefreshToken, TestContext, default));
        Assert.NotNull(await setup.Service.LoginAsync(new("persona@correo.gt", "Nueva123!"), TestContext, default));
    }

    [Fact]
    public async Task PasswordReset_RejectsExpiredToken()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        await setup.Service.RequestPasswordRecoveryAsync("persona@correo.gt", TestContext, default);
        var recovery = await setup.Db.PasswordRecoveryTokens.SingleAsync();
        recovery.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        await setup.Db.SaveChangesAsync();

        Assert.False(await setup.Service.ResetPasswordAsync(new(setup.Mail.LastToken, "Nueva123!"), TestContext, default));
    }

    [Fact]
    public async Task ForgotPassword_DoesNotRevealWhetherEmailExists()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var controller = new AuthController(setup.Service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var existing = Assert.IsType<AcceptedResult>((await controller.Forgot(new("persona@correo.gt"), default)).Result);
        var missing = Assert.IsType<AcceptedResult>((await controller.Forgot(new("nadie@correo.gt"), default)).Result);
        var existingBody = Assert.IsType<ForgotPasswordResponse>(existing.Value);
        var missingBody = Assert.IsType<ForgotPasswordResponse>(missing.Value);
        Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal(existingBody.Message, missingBody.Message);
    }

    [Fact]
    public async Task SmtpFailure_IsLogged_AndForgotResponseRemainsNeutral()
    {
        var logger = new CapturingLogger();
        var setup = Create(new FailingEmailSender(), logger);
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var controller = new AuthController(setup.Service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var existing = Assert.IsType<AcceptedResult>((await controller.Forgot(new("persona@correo.gt"), default)).Result);
        var missing = Assert.IsType<AcceptedResult>((await controller.Forgot(new("nadie@correo.gt"), default)).Result);

        Assert.Equal(Assert.IsType<ForgotPasswordResponse>(existing.Value).Message, Assert.IsType<ForgotPasswordResponse>(missing.Value).Message);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("535 5.7.8 Authentication rejected", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("app-password", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public async Task SmtpTimeout_IsLogged_AndForgotResponseRemainsNeutral()
    {
        var logger = new CapturingLogger();
        var setup = Create(new TimeoutEmailSender(), logger);
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var controller = new AuthController(setup.Service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var result = Assert.IsType<AcceptedResult>((await controller.Forgot(new("persona@correo.gt"), default)).Result);

        Assert.Equal("Si el correo está registrado, recibirás un enlace en unos minutos.", Assert.IsType<ForgotPasswordResponse>(result.Value).Message);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("SMTP timeout simulado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdminReset_RequiresForcedChange_AndNeverAuditsTemporaryPassword()
    {
        var setup = Create();
        var target = await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var oldSession = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);
        var temporary = await setup.Service.AdminResetPasswordAsync(Guid.NewGuid(), target.Id, TestContext, default);

        Assert.NotNull(temporary);
        Assert.Null(await setup.Service.RefreshAsync(oldSession!.RefreshToken, TestContext, default));
        var restricted = await setup.Service.LoginAsync(new("persona@correo.gt", temporary!), TestContext, default);
        Assert.True(restricted!.User.MustChangePassword);

        var completed = await setup.Service.ChangeRequiredPasswordAsync(target.Id, new(temporary!, "Definitiva123!"), TestContext, default);
        Assert.NotNull(completed);
        Assert.False(completed!.User.MustChangePassword);
        Assert.NotNull(await setup.Service.LoginAsync(new("persona@correo.gt", "Definitiva123!"), TestContext, default));
        var audit = await setup.Db.AccessAudits.SingleAsync(x => x.EventType == "PASSWORD_ADMIN_RESET");
        Assert.DoesNotContain(temporary!, audit.Detail, StringComparison.Ordinal);
    }

    private static Setup Create(IEmailSender? sender = null, ILogger<AuthService>? logger = null)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(
            new Role { Name = RoleNames.Administrator, NormalizedName = RoleNames.Administrator.ToUpperInvariant(), Description = "Admin" },
            new Role { Name = RoleNames.Technician, NormalizedName = RoleNames.Technician.ToUpperInvariant(), Description = "Técnico" },
            new Role { Name = RoleNames.Operator, NormalizedName = RoleNames.Operator.ToUpperInvariant(), Description = "Operador" });
        db.SaveChanges();

        var mail = new FakeEmailSender();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(db);
        services.AddSingleton<IEmailSender>(sender ?? mail);
        if (logger is not null) services.AddSingleton(logger);
        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(new JwtOptions { Issuer = "tests", Audience = "tests", SigningKey = "clave-de-pruebas-con-mas-de-32-caracteres-segura", AccessTokenMinutes = 15, RefreshTokenDays = 7, PasswordRecoveryMinutes = 30 }));
        services.AddSingleton<IOptions<EmailOptions>>(Options.Create(new EmailOptions { FrontendBaseUrl = "http://localhost:5173" }));
        services.AddIdentityCore<User>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
        }).AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        services.AddScoped<AuthService>();
        var provider = services.BuildServiceProvider();
        return new(provider.GetRequiredService<AuthService>(), db, mail);
    }

    private sealed record Setup(AuthService Service, AppDbContext Db, FakeEmailSender Mail);

    private sealed class FakeEmailSender : IEmailSender
    {
        public string LastLink { get; private set; } = string.Empty;
        public string LastToken => Uri.UnescapeDataString(LastLink[(LastLink.IndexOf("?resetToken=", StringComparison.Ordinal) + 12)..]);
        public Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct)
        {
            LastLink = resetLink;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct) =>
            throw new System.Net.Mail.SmtpException("535 5.7.8 Authentication rejected");
    }

    private sealed class TimeoutEmailSender : IEmailSender
    {
        public Task SendPasswordRecoveryAsync(string recipient, string displayName, string resetLink, DateTime expiresAtUtc, CancellationToken ct) =>
            throw new TimeoutException("SMTP timeout simulado");
    }

    private sealed class CapturingLogger : ILogger<AuthService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " · " + exception.Message)));
    }
}