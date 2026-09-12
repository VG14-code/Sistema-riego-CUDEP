using System.IdentityModel.Tokens.Jwt;
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
        var response = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))?.Session;
        Assert.NotNull(response);
        Assert.Contains('.', response!.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public async Task Login_EmbedsEffectivePermissions_AsJwtClaims()
    {
        var setup = Create();
        var permission = new Permission { Code = "riego.operar", Description = "Operar riego" };
        setup.Db.Permissions.Add(permission);
        await setup.Db.SaveChangesAsync();
        var operatorRole = await setup.Db.Roles.SingleAsync(x => x.Name == RoleNames.Operator);
        setup.Db.RolePermissions.Add(new RolePermission { RoleId = operatorRole.Id, PermissionId = permission.Id });
        await setup.Db.SaveChangesAsync();

        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona de prueba"), default);
        var response = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))?.Session;

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response!.AccessToken);
        Assert.Contains(token.Claims, c => c.Type == "perm" && c.Value == "riego.operar");
    }

    [Fact]
    public async Task Login_RejectsInvalidPassword_AndAuditsAttempt()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        Assert.Null((await setup.Service.LoginAsync(new("persona@correo.gt", "incorrecta"), TestContext, default))?.Session);
        Assert.Equal(1, await setup.Db.AccessAudits.CountAsync(x => x.EventType == "LOGIN_FAILED"));
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndRevokesPreviousSession()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var login = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))?.Session;
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
        var login = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))?.Session;
        Assert.True(await setup.Service.LogoutAsync(login!.RefreshToken, default));
        Assert.False(await setup.Service.LogoutAsync(login.RefreshToken, default));
    }

    [Fact]
    public async Task PasswordReset_IsOneUse_ChangesPassword_AndInvalidatesSessions()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var oldSession = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))?.Session;
        await setup.Service.RequestPasswordRecoveryAsync("persona@correo.gt", TestContext, default);
        var token = setup.Mail.LastToken;

        Assert.True(await setup.Service.ResetPasswordAsync(new(token, "Nueva123!"), TestContext, default));
        Assert.False(await setup.Service.ResetPasswordAsync(new(token, "Otra123!"), TestContext, default));
        Assert.Null(await setup.Service.RefreshAsync(oldSession!.RefreshToken, TestContext, default));
        Assert.NotNull((await setup.Service.LoginAsync(new("persona@correo.gt", "Nueva123!"), TestContext, default))?.Session);
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
    public async Task LoginSetsHttpOnlyCookie_AndRefreshAcceptsCookie()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var loginContext = new DefaultHttpContext();
        var loginController = new AuthController(setup.Service) { ControllerContext = new ControllerContext { HttpContext = loginContext } };

        var login = Assert.IsType<OkObjectResult>(await loginController.Login(new("persona@correo.gt", "Segura123!"), default));
        var session = Assert.IsType<AuthResponse>(login.Value);
        var setCookie = loginContext.Response.Headers.SetCookie.ToString();
        Assert.Contains("riego.refresh=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        var refreshContext = new DefaultHttpContext();
        refreshContext.Request.Headers.Cookie = $"riego.refresh={session.RefreshToken}";
        var refreshController = new AuthController(setup.Service) { ControllerContext = new ControllerContext { HttpContext = refreshContext } };
        var refreshed = Assert.IsType<OkObjectResult>((await refreshController.Refresh(new(null), default)).Result);
        Assert.IsType<AuthResponse>(refreshed.Value);
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
        var oldSession = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))?.Session;
        var temporary = await setup.Service.AdminResetPasswordAsync(Guid.NewGuid(), target.Id, TestContext, default);

        Assert.NotNull(temporary);
        Assert.Null(await setup.Service.RefreshAsync(oldSession!.RefreshToken, TestContext, default));
        var restricted = (await setup.Service.LoginAsync(new("persona@correo.gt", temporary!), TestContext, default))?.Session;
        Assert.True(restricted!.User.MustChangePassword);

        var completed = await setup.Service.ChangeRequiredPasswordAsync(target.Id, new(temporary!, "Definitiva123!"), TestContext, default);
        Assert.NotNull(completed);
        Assert.False(completed!.User.MustChangePassword);
        Assert.NotNull((await setup.Service.LoginAsync(new("persona@correo.gt", "Definitiva123!"), TestContext, default))?.Session);
        var audit = await setup.Db.AccessAudits.SingleAsync(x => x.EventType == "PASSWORD_ADMIN_RESET");
        Assert.DoesNotContain(temporary!, audit.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithTwoFactorEnabled_IssuesChallengeInsteadOfSession()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        await EnableTwoFactor(setup);

        var result = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);

        // La contraseña correcta no debe producir token de acceso por si sola.
        Assert.NotNull(result);
        Assert.Null(result!.Session);
        Assert.NotNull(result.Challenge);
        Assert.True(result.Challenge!.RequiresTwoFactor);
        Assert.True(result.Challenge.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Empty(await setup.Db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task CompleteTwoFactorLogin_WithValidCode_CreatesSessionAndConsumesChallenge()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var user = await EnableTwoFactor(setup);
        var challenge = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))!.Challenge!;
        var code = await CurrentTotpAsync(setup, user);

        var session = await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", challenge.ChallengeToken, code), TestContext, default);

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session!.AccessToken));
        Assert.Single(await setup.Db.Sessions.ToListAsync());
        // El desafio es de un solo uso: reutilizarlo no debe abrir otra sesion.
        Assert.Null(await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", challenge.ChallengeToken, code), TestContext, default));
        Assert.Single(await setup.Db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task CompleteTwoFactorLogin_WithWrongCode_DoesNotCreateSession()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        await EnableTwoFactor(setup);
        var challenge = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))!.Challenge!;

        Assert.Null(await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", challenge.ChallengeToken, "000000"), TestContext, default));
        Assert.Empty(await setup.Db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task CompleteTwoFactorLogin_StopsAfterFiveFailedAttempts()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var user = await EnableTwoFactor(setup);
        var challenge = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))!.Challenge!;

        for (var i = 0; i < 5; i++)
            Assert.Null(await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", challenge.ChallengeToken, "000000"), TestContext, default));

        // Agotados los intentos el desafio queda quemado, aunque el codigo sea correcto.
        var valid = await CurrentTotpAsync(setup, user);
        Assert.Null(await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", challenge.ChallengeToken, valid), TestContext, default));
        Assert.Empty(await setup.Db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task CompleteTwoFactorLogin_RejectsChallengeIssuedForAnotherAccount()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        await setup.Service.RegisterAsync(new("otra@correo.gt", "Segura123!", "Otra persona"), default);
        var user = await EnableTwoFactor(setup);
        var challenge = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))!.Challenge!;
        var code = await CurrentTotpAsync(setup, user);

        Assert.Null(await setup.Service.CompleteTwoFactorLoginAsync(new("otra@correo.gt", challenge.ChallengeToken, code), TestContext, default));
        Assert.Empty(await setup.Db.Sessions.ToListAsync());
    }

    [Fact]
    public async Task CompleteTwoFactorLogin_AcceptsRecoveryCodeOnce()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        var user = await EnableTwoFactor(setup);
        var recoveryCodes = (await setup.Users.GenerateNewTwoFactorRecoveryCodesAsync(user, 3))!.ToArray();
        var challenge = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))!.Challenge!;

        Assert.NotNull(await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", challenge.ChallengeToken, recoveryCodes[0]), TestContext, default));

        // El mismo codigo de recuperacion no sirve dos veces.
        var second = (await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default))!.Challenge!;
        Assert.Null(await setup.Service.CompleteTwoFactorLoginAsync(new("persona@correo.gt", second.ChallengeToken, recoveryCodes[0]), TestContext, default));
    }

    [Fact]
    public async Task Login_WithoutTwoFactor_KeepsReturningSessionDirectly()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);

        var result = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);

        Assert.NotNull(result!.Session);
        Assert.Null(result.Challenge);
    }

    // El proveedor de autenticador de Identity no genera codigos desde el servidor:
    // GenerateTwoFactorTokenAsync devuelve cadena vacia porque el codigo lo produce la
    // app del usuario. La prueba calcula el TOTP igual que lo haria esa app.
    private static async Task<string> CurrentTotpAsync(Setup setup, User user)
    {
        var key = await setup.Users.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("El usuario no tiene clave TOTP.");
        var secret = Base32Decode(key);
        var timestep = (long)((DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).TotalSeconds / 30);
        var counter = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, timestep);
        using var hmac = new System.Security.Cryptography.HMACSHA1(secret);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16) | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var clean = value.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int bits = 0, accumulator = 0;
        foreach (var character in clean)
        {
            var index = alphabet.IndexOf(character);
            if (index < 0) throw new FormatException($"Caracter invalido en base32: {character}");
            accumulator = (accumulator << 5) | index;
            bits += 5;
            if (bits < 8) continue;
            output.Add((byte)((accumulator >> (bits - 8)) & 0xFF));
            bits -= 8;
        }
        return output.ToArray();
    }

    private static async Task<User> EnableTwoFactor(Setup setup)
    {
        var user = await setup.Users.FindByEmailAsync("persona@correo.gt") ?? throw new InvalidOperationException("Usuario de prueba no encontrado.");
        await setup.Users.ResetAuthenticatorKeyAsync(user);
        await setup.Users.SetTwoFactorEnabledAsync(user, true);
        return user;
    }

    [Fact]
    public async Task MaxLoginAttempts_LocksAccountAtTheConfiguredThreshold()
    {
        var setup = Create();
        await setup.Service.RegisterAsync(new("persona@correo.gt", "Segura123!", "Persona"), default);
        setup.Db.GlobalParameters.Add(new GlobalParameter { Key = "MAX_LOGIN_ATTEMPTS", Value = "2", DataType = "integer", Category = "Seguridad", Description = "Prueba" });
        await setup.Db.SaveChangesAsync();

        // Identity bloquearia a los cinco intentos; el parametro endurece el limite a dos.
        Assert.Null(await setup.Service.LoginAsync(new("persona@correo.gt", "incorrecta"), TestContext, default));
        Assert.Null(await setup.Service.LoginAsync(new("persona@correo.gt", "incorrecta"), TestContext, default));

        var user = await setup.Users.FindByEmailAsync("persona@correo.gt");
        Assert.True(await setup.Users.IsLockedOutAsync(user!));
        // Ni con la contrasena correcta se entra mientras dure el bloqueo.
        Assert.Null(await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default));
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
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        var provider = services.BuildServiceProvider();
        return new(provider.GetRequiredService<AuthService>(), db, mail, provider.GetRequiredService<UserManager<User>>());
    }

    private sealed record Setup(AuthService Service, AppDbContext Db, FakeEmailSender Mail, UserManager<User> Users);

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