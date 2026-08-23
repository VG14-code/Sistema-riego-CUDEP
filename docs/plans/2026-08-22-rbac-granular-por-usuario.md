# RBAC Granular Independiente por Usuario — Plan de Implementación

**Goal:** Permitir conceder o revocar permisos puntuales a un usuario específico, de forma independiente de su(s) rol(es), sin romper la autorización por rol que ya protege ~30 controllers.

**Architecture:** Se añade una tabla `UserPermission` (override por usuario, con `IsGranted` true/false) que se combina con los permisos heredados de `RolePermission` para formar el conjunto de "permisos efectivos" de un usuario (`override` siempre gana sobre el rol). Ese conjunto se embebe como claims `perm` en el JWT al emitir sesión (igual patrón que los claims de rol hoy). La autorización de endpoints migra de `RequireRole` a un `IAuthorizationHandler` genérico que evalúa esos claims. La migración de los ~30 controllers existentes se hace mediante un piloto completo (`DeviceCatalogsController`) que deja la receta lista para el resto (fuera de este plan, ver "Fuera de alcance").

**Tech Stack:** ASP.NET Core 10 (Identity Core + JWT Bearer), EF Core 10 / SQL Server, xUnit + EF InMemory (sin mocks, patrón ya usado en `AuthServiceTests.cs` / `Week2ControllerTests.cs`), React + TypeScript en el frontend (`week2Api.ts`, `Sprint2Administration.tsx`).

**Convenciones observadas en el repo (seguir, no reinventar):**
- Los tests de controller invocan el controller directamente (sin `WebApplicationFactory`), inyectando un `ClaimsPrincipal` a mano — ver `Week2ControllerTests.WithUser`. No se prueba el pipeline `[Authorize]` con tests de integración HTTP; se prueba el `IAuthorizationHandler` de forma aislada.
- Operaciones críticas (cambiar roles/estado/permisos) exigen TOTP vía `ITotpService? totp = null` inyectado como opcional, y quedan registradas en `AccessAudit`.
- Migraciones EF se generan con `dotnet ef migrations add <Nombre>` desde `backend/SistemaRiego.Api`.

---

## Fuera de alcance de este plan

Migrar los ~30 controllers restantes de `[Authorize(Policy = Policies.X)]` (rol) a políticas de permiso es mecánico una vez completado el piloto (Fase 7), pero enumerar los ~100+ endpoints uno por uno aquí infla el plan sin valor adicional. Al cerrar Fase 7 quedará la receta de 4 pasos (definir códigos → mapear a `AddPolicy` → sustituir atributos → mover permisos en el seed) para aplicarla controller por controller como trabajo de seguimiento.

---

## Fase 1: Modelo de datos — `UserPermission`

**Files:**
- Modify: `backend/SistemaRiego.Api/Models/Entities.cs`
- Modify: `backend/SistemaRiego.Api/Data/AppDbContext.cs`
- Test: `backend/SistemaRiego.Api.Tests/Modules7To10ControllerTests.cs` no aplica — crear `backend/SistemaRiego.Api.Tests/UserPermissionModelTests.cs`

**Step 1: Escribir el test que falla (el modelo aún no existe)**

Crear `backend/SistemaRiego.Api.Tests/UserPermissionModelTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class UserPermissionModelTests
{
    [Fact]
    public async Task UserPermission_PersistsOverride_WithCompositeKey()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "u@correo.gt", UserName = "u@correo.gt", FullName = "U" };
        var permission = new Permission { Code = "prueba.codigo", Description = "Prueba" };
        var actor = Guid.NewGuid();
        db.Users.Add(user); db.Permissions.Add(permission);
        await db.SaveChangesAsync();

        db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = permission.Id, IsGranted = true, GrantedByUserId = actor });
        await db.SaveChangesAsync();

        var stored = await db.UserPermissions.SingleAsync();
        Assert.True(stored.IsGranted);
        Assert.Equal(actor, stored.GrantedByUserId);
    }
}
```

**Step 2: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter UserPermission_PersistsOverride_WithCompositeKey`
Expected: FAIL — `error CS0117: 'AppDbContext' does not contain a definition for 'UserPermissions'` (no compila todavía).

**Step 3: Añadir la entidad**

En `backend/SistemaRiego.Api/Models/Entities.cs`, después de la clase `RolePermission` (línea 59), agregar:

```csharp
public sealed class UserPermission
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
    public bool IsGranted { get; set; }
    public Guid GrantedByUserId { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
```

En la misma clase `User` (línea 16), añadir la colección junto a `UserRoles`:

```csharp
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
```

En la clase `Permission` (línea 43), añadir junto a `RolePermissions`:

```csharp
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
```

**Step 4: Registrar el DbSet y el mapeo**

En `backend/SistemaRiego.Api/Data/AppDbContext.cs:9`, cambiar:

```csharp
public DbSet<Permission> Permissions => Set<Permission>(); public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
```

por:

```csharp
public DbSet<Permission> Permissions => Set<Permission>(); public DbSet<RolePermission> RolePermissions => Set<RolePermission>(); public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
```

En `OnModelCreating`, línea 34, después de `b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });` agregar:

```csharp
        b.Entity<UserPermission>(e => { e.HasKey(x => new { x.UserId, x.PermissionId }); e.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Permission).WithMany(x => x.UserPermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade); });
```

**Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter UserPermission_PersistsOverride_WithCompositeKey`
Expected: PASS

**Step 6: Generar la migración EF (contra SQL Server real, no InMemory)**

Run (desde la raíz del repo):
```bash
dotnet ef migrations add AddUserPermissions --project backend/SistemaRiego.Api/SistemaRiego.Api.csproj --startup-project backend/SistemaRiego.Api/SistemaRiego.Api.csproj
```
Expected: nuevo archivo en `backend/SistemaRiego.Api/Data/Migrations/<timestamp>_AddUserPermissions.cs` con `CreateTable("UserPermissions", ...)`. Revisar el archivo generado antes de continuar — confirmar que la FK a `AspNetUsers`/`Permissions` usa `ON DELETE CASCADE` en ambas puntas.

**Step 7: Commit**

```bash
git add backend/SistemaRiego.Api/Models/Entities.cs backend/SistemaRiego.Api/Data/AppDbContext.cs backend/SistemaRiego.Api/Data/Migrations backend/SistemaRiego.Api.Tests/UserPermissionModelTests.cs
git commit -m "feat: agregar modelo UserPermission para overrides por usuario"
```

---

## Fase 2: Catálogo de permisos finos para el piloto

**Files:**
- Create: `backend/SistemaRiego.Api/Models/PermissionCodes.cs`
- Modify: `backend/SistemaRiego.Api/Data/DbSeeder.cs:25-33`
- Test: `backend/SistemaRiego.Api.Tests/DbSeederPermissionTests.cs` (nuevo)

**Step 1: Escribir el test que falla**

Crear `backend/SistemaRiego.Api.Tests/DbSeederPermissionTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class DbSeederPermissionTests
{
    [Fact]
    public async Task Seed_CreatesDeviceCatalogPermissions_AndAssignsThemByRole()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();
        services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>();
        var provider = services.BuildServiceProvider();
        var configuration = new ConfigurationBuilder().Build();

        await DbSeeder.SeedAsync(provider, configuration);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Permissions.AnyAsync(x => x.Code == PermissionCodes.DeviceCatalogsRead));
        Assert.True(await db.Permissions.AnyAsync(x => x.Code == PermissionCodes.DeviceCatalogsManage));
        Assert.True(await db.Permissions.AnyAsync(x => x.Code == PermissionCodes.DeviceCatalogsDelete));

        var operatorRole = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == RoleNames.Operator);
        Assert.Contains(operatorRole.RolePermissions, x => x.Permission.Code == PermissionCodes.DeviceCatalogsRead);
        Assert.DoesNotContain(operatorRole.RolePermissions, x => x.Permission.Code == PermissionCodes.DeviceCatalogsDelete);

        var adminRole = await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == RoleNames.Administrator);
        Assert.Contains(adminRole.RolePermissions, x => x.Permission.Code == PermissionCodes.DeviceCatalogsDelete);
    }
}
```

**Step 2: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter Seed_CreatesDeviceCatalogPermissions_AndAssignsThemByRole`
Expected: FAIL — `PermissionCodes` no existe.

**Step 3: Crear el catálogo de códigos**

Crear `backend/SistemaRiego.Api/Models/PermissionCodes.cs`:

```csharp
namespace SistemaRiego.Api.Models;

public static class PermissionCodes
{
    public const string UsersRead = "usuarios.leer";
    public const string UsersManage = "usuarios.gestionar";
    public const string IrrigationOperate = "riego.operar";
    public const string DevicesManage = "dispositivos.gestionar";
    public const string ReportsRead = "reportes.leer";
    public const string DeviceCatalogsRead = "dispositivos.catalogos.leer";
    public const string DeviceCatalogsManage = "dispositivos.catalogos.gestionar";
    public const string DeviceCatalogsDelete = "dispositivos.catalogos.eliminar";
}
```

**Step 4: Extender el seed**

En `backend/SistemaRiego.Api/Data/DbSeeder.cs:25`, reemplazar:

```csharp
        var definitions = new[] { ("usuarios.leer", "Consultar usuarios"), ("usuarios.gestionar", "Gestionar usuarios, roles y estados"), ("riego.operar", "Operar el sistema de riego"), ("dispositivos.gestionar", "Configurar sensores y dispositivos"), ("reportes.leer", "Consultar reportes") };
```

por:

```csharp
        var definitions = new[]
        {
            (PermissionCodes.UsersRead, "Consultar usuarios"),
            (PermissionCodes.UsersManage, "Gestionar usuarios, roles y estados"),
            (PermissionCodes.IrrigationOperate, "Operar el sistema de riego"),
            (PermissionCodes.DevicesManage, "Configurar sensores y dispositivos"),
            (PermissionCodes.ReportsRead, "Consultar reportes"),
            (PermissionCodes.DeviceCatalogsRead, "Consultar marcas y modelos de dispositivos"),
            (PermissionCodes.DeviceCatalogsManage, "Crear y editar marcas y modelos de dispositivos"),
            (PermissionCodes.DeviceCatalogsDelete, "Eliminar marcas y modelos de dispositivos")
        };
```

Y en `DbSeeder.cs:28-33`, reemplazar el arreglo `roleDefinitions` por:

```csharp
        var roleDefinitions = new[]
        {
            (RoleNames.Administrator, "Control total del sistema", definitions.Select(x => x.Item1).ToArray()),
            (RoleNames.Technician, "Configuración técnica y consulta", new[] { PermissionCodes.UsersRead, PermissionCodes.DevicesManage, PermissionCodes.ReportsRead, PermissionCodes.DeviceCatalogsRead, PermissionCodes.DeviceCatalogsManage }),
            (RoleNames.Operator, "Operación cotidiana del riego", new[] { PermissionCodes.IrrigationOperate, PermissionCodes.ReportsRead, PermissionCodes.DeviceCatalogsRead })
        };
```

Esto preserva exactamente el comportamiento actual de `DeviceCatalogsController` (Operator=lee, Technician=crea/edita, Administrator=elimina) cuando se migre en la Fase 7.

**Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter Seed_CreatesDeviceCatalogPermissions_AndAssignsThemByRole`
Expected: PASS

**Step 6: Correr toda la suite para descartar regresiones en el seed**

Run: `dotnet test backend/SistemaRiego.Api.Tests`
Expected: PASS (todo verde; ningún test existente depende de los textos literales de `definitions`)

**Step 7: Commit**

```bash
git add backend/SistemaRiego.Api/Models/PermissionCodes.cs backend/SistemaRiego.Api/Data/DbSeeder.cs backend/SistemaRiego.Api.Tests/DbSeederPermissionTests.cs
git commit -m "feat: agregar catalogo de permisos finos para dispositivos.catalogos"
```

---

## Fase 3: Servicio `IPermissionResolver` (permisos efectivos)

**Files:**
- Create: `backend/SistemaRiego.Api/Services/IPermissionResolver.cs`
- Create: `backend/SistemaRiego.Api/Services/PermissionResolver.cs`
- Modify: `backend/SistemaRiego.Api/Program.cs`
- Test: `backend/SistemaRiego.Api.Tests/PermissionResolverTests.cs`

**Step 1: Escribir el test que falla**

Crear `backend/SistemaRiego.Api.Tests/PermissionResolverTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class PermissionResolverTests
{
    [Fact]
    public async Task Effective_CombinesRolePermissions_WithGrantOverride()
    {
        var db = NewDb();
        var permission = new Permission { Code = "extra.permiso", Description = "Extra" };
        var role = new Role { Name = "Operador", NormalizedName = "OPERADOR" };
        var user = new User { Email = "u@correo.gt", UserName = "u@correo.gt", FullName = "U" };
        db.Permissions.Add(permission); db.Roles.Add(role); db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = permission.Id, IsGranted = true, GrantedByUserId = user.Id });
        await db.SaveChangesAsync();

        var effective = await new PermissionResolver(db).GetEffectivePermissionsAsync(user.Id, default);

        Assert.Contains("extra.permiso", effective);
    }

    [Fact]
    public async Task Effective_RevokeOverride_RemovesRolePermission()
    {
        var db = NewDb();
        var permission = new Permission { Code = "riego.operar", Description = "Operar riego" };
        var role = new Role { Name = "Operador", NormalizedName = "OPERADOR" };
        var user = new User { Email = "u2@correo.gt", UserName = "u2@correo.gt", FullName = "U2" };
        db.Permissions.Add(permission); db.Roles.Add(role); db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionId = permission.Id, IsGranted = false, GrantedByUserId = user.Id });
        await db.SaveChangesAsync();

        var effective = await new PermissionResolver(db).GetEffectivePermissionsAsync(user.Id, default);

        Assert.DoesNotContain("riego.operar", effective);
    }

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
```

**Step 2: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter PermissionResolverTests`
Expected: FAIL — `PermissionResolver` no existe.

**Step 3: Implementar la interfaz y el servicio**

Crear `backend/SistemaRiego.Api/Services/IPermissionResolver.cs`:

```csharp
namespace SistemaRiego.Api.Services;

public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct);
}
```

Crear `backend/SistemaRiego.Api/Services/PermissionResolver.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SistemaRiego.Api.Data;

namespace SistemaRiego.Api.Services;

public sealed class PermissionResolver(AppDbContext db) : IPermissionResolver
{
    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct)
    {
        var fromRoles = await db.UserRoles.Where(x => x.UserId == userId)
            .SelectMany(x => x.Role.RolePermissions.Select(y => y.Permission.Code))
            .ToListAsync(ct);
        var overrides = await db.UserPermissions.Where(x => x.UserId == userId)
            .Select(x => new { x.Permission.Code, x.IsGranted })
            .ToListAsync(ct);

        var effective = new HashSet<string>(fromRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var o in overrides)
        {
            if (o.IsGranted) effective.Add(o.Code);
            else effective.Remove(o.Code);
        }
        return effective;
    }
}
```

**Step 4: Registrar en DI**

En `backend/SistemaRiego.Api/Program.cs`, junto a `builder.Services.AddScoped<IAuthService, AuthService>();` (línea 65), agregar:

```csharp
builder.Services.AddScoped<IPermissionResolver, PermissionResolver>();
```

**Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter PermissionResolverTests`
Expected: PASS (2 tests)

**Step 6: Commit**

```bash
git add backend/SistemaRiego.Api/Services/IPermissionResolver.cs backend/SistemaRiego.Api/Services/PermissionResolver.cs backend/SistemaRiego.Api/Program.cs backend/SistemaRiego.Api.Tests/PermissionResolverTests.cs
git commit -m "feat: resolver permisos efectivos (rol + overrides por usuario)"
```

---

## Fase 4: Autorización basada en permisos (`PermissionRequirement`)

Los permisos efectivos se embeben como claims `perm` en el JWT (Fase 5), así que el handler solo lee el `ClaimsPrincipal` — no toca la base de datos en cada request.

**Files:**
- Create: `backend/SistemaRiego.Api/Services/PermissionAuthorization.cs`
- Modify: `backend/SistemaRiego.Api/Models/Security.cs`
- Modify: `backend/SistemaRiego.Api/Program.cs`
- Test: `backend/SistemaRiego.Api.Tests/PermissionAuthorizationHandlerTests.cs`

**Step 1: Escribir el test que falla**

Crear `backend/SistemaRiego.Api.Tests/PermissionAuthorizationHandlerTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_WhenUserHasMatchingPermClaim()
    {
        var requirement = new PermissionRequirement("dispositivos.catalogos.eliminar");
        var identity = new ClaimsIdentity([new Claim("perm", "dispositivos.catalogos.eliminar")], "tests");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_WhenUserLacksPermClaim()
    {
        var requirement = new PermissionRequirement("dispositivos.catalogos.eliminar");
        var identity = new ClaimsIdentity([new Claim("perm", "dispositivos.catalogos.leer")], "tests");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
```

**Step 2: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter PermissionAuthorizationHandlerTests`
Expected: FAIL — `PermissionRequirement`/`PermissionAuthorizationHandler` no existen.

**Step 3: Implementar el requirement y el handler**

Crear `backend/SistemaRiego.Api/Services/PermissionAuthorization.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace SistemaRiego.Api.Services;

public sealed class PermissionRequirement(string code) : IAuthorizationRequirement
{
    public string Code { get; } = code;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("perm", requirement.Code)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

**Step 4: Definir las políticas de permiso para el piloto**

En `backend/SistemaRiego.Api/Models/Security.cs:3`, después de la línea de `Policies`, agregar:

```csharp
public static class PermissionPolicies
{
    public const string DeviceCatalogsRead = "Permiso:DispositivosCatalogosLeer";
    public const string DeviceCatalogsManage = "Permiso:DispositivosCatalogosGestionar";
    public const string DeviceCatalogsDelete = "Permiso:DispositivosCatalogosEliminar";
}
```

**Step 5: Registrar el handler y las políticas**

En `backend/SistemaRiego.Api/Program.cs`, agregar junto a los demás `AddScoped` (cerca de línea 65):

```csharp
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
```

Y en el bloque `AddAuthorization` (línea 109-114), agregar después de las 3 políticas existentes:

```csharp
    o.AddPolicy(PermissionPolicies.DeviceCatalogsRead, p => p.Requirements.Add(new PermissionRequirement(PermissionCodes.DeviceCatalogsRead)));
    o.AddPolicy(PermissionPolicies.DeviceCatalogsManage, p => p.Requirements.Add(new PermissionRequirement(PermissionCodes.DeviceCatalogsManage)));
    o.AddPolicy(PermissionPolicies.DeviceCatalogsDelete, p => p.Requirements.Add(new PermissionRequirement(PermissionCodes.DeviceCatalogsDelete)));
```

Nota: esto es **aditivo** — las 3 políticas por rol (`Policies.Administrator/Technician/Operator`) siguen intactas, así que ningún controller existente cambia de comportamiento todavía.

**Step 6: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter PermissionAuthorizationHandlerTests`
Expected: PASS (2 tests)

Run también: `dotnet build backend/SistemaRiego.Api` para confirmar que `Program.cs` compila con los nuevos `using`/tipos.

**Step 7: Commit**

```bash
git add backend/SistemaRiego.Api/Services/PermissionAuthorization.cs backend/SistemaRiego.Api/Models/Security.cs backend/SistemaRiego.Api/Program.cs backend/SistemaRiego.Api.Tests/PermissionAuthorizationHandlerTests.cs
git commit -m "feat: autorizacion basada en permisos via claim perm"
```

---

## Fase 5: Embeber permisos efectivos en el JWT + invalidación inmediata

**Files:**
- Modify: `backend/SistemaRiego.Api/Services/AuthService.cs`
- Modify: `backend/SistemaRiego.Api.Tests/AuthServiceTests.cs`

**Step 1: Escribir el test que falla**

En `backend/SistemaRiego.Api.Tests/AuthServiceTests.cs`, agregar (después de `Login_ReturnsJwtAndRefreshToken_ForValidCredentials`, ~línea 39):

```csharp
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
        var response = await setup.Service.LoginAsync(new("persona@correo.gt", "Segura123!"), TestContext, default);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(response!.AccessToken);
        Assert.Contains(token.Claims, c => c.Type == "perm" && c.Value == "riego.operar");
    }
```

Añadir `using System.IdentityModel.Tokens.Jwt;` al tope del archivo si no está ya (revisar imports actuales del archivo).

**Step 2: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter Login_EmbedsEffectivePermissions_AsJwtClaims`
Expected: FAIL — no hay claims `perm` en el token todavía.

**Step 3: Inyectar `IPermissionResolver` en `AuthService` y agregar los claims**

En `backend/SistemaRiego.Api/Services/AuthService.cs:15-21`, cambiar la firma del constructor:

```csharp
public sealed class AuthService(
    AppDbContext db,
    IOptions<JwtOptions> options,
    IOptions<EmailOptions> emailOptions,
    ILogger<AuthService> logger,
    UserManager<User> userManager,
    IEmailSender emailSender,
    IPermissionResolver permissionResolver) : IAuthService
```

En `CreateSession` (línea 164-187), después de la línea `var roles = user.UserRoles...` (línea 173), agregar:

```csharp
        var permissions = await permissionResolver.GetEffectivePermissionsAsync(user.Id, ct);
```

Y después de `claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));` (línea 184), agregar:

```csharp
        claims.AddRange(permissions.Select(x => new Claim("perm", x)));
```

**Step 4: Actualizar el fixture de test para registrar `IPermissionResolver`**

En `backend/SistemaRiego.Api.Tests/AuthServiceTests.cs`, dentro de `Create()` (línea 165-195), agregar junto a `services.AddScoped<AuthService>();` (línea 192):

```csharp
        services.AddScoped<IPermissionResolver, PermissionResolver>();
```

**Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter Login_EmbedsEffectivePermissions_AsJwtClaims`
Expected: PASS

Run: `dotnet test backend/SistemaRiego.Api.Tests` completo para confirmar que ningún otro test de `AuthService`/controllers que instancian `AuthService` a mano se rompió por el nuevo parámetro del constructor.

Run: `grep -rn "new AuthService(" backend/SistemaRiego.Api.Tests` — si aparece alguna instanciación manual fuera de `AuthServiceTests.Create()`, actualizarla igual (agregar el `IPermissionResolver`).

**Step 6: Commit**

```bash
git add backend/SistemaRiego.Api/Services/AuthService.cs backend/SistemaRiego.Api.Tests/AuthServiceTests.cs
git commit -m "feat: incluir permisos efectivos como claims perm en el JWT"
```

---

## Fase 6: API de permisos por usuario (`GET`/`PUT /api/users/{id}/permissions`)

Reutiliza el patrón TOTP + auditoría ya presente en `UsersController` (`VerifyTotp`, `AccessAudit`).

**Files:**
- Create: `backend/SistemaRiego.Api/Contracts/UserPermissionContracts.cs`
- Modify: `backend/SistemaRiego.Api/Controllers/UsersController.cs`
- Test: `backend/SistemaRiego.Api.Tests/UsersControllerPermissionsTests.cs`

**Step 1: Contratos**

Crear `backend/SistemaRiego.Api/Contracts/UserPermissionContracts.cs`:

```csharp
namespace SistemaRiego.Api.Contracts;

public sealed record EffectivePermissionResponse(string Code, string Description, bool GrantedByRole, bool? OverrideIsGranted, bool EffectiveGranted);
public sealed record PermissionOverrideRequest(string Code, bool IsGranted);
public sealed record UpdateUserPermissionsRequest(IReadOnlyCollection<PermissionOverrideRequest> Overrides);
```

**Step 2: Escribir los tests que fallan**

Crear `backend/SistemaRiego.Api.Tests/UsersControllerPermissionsTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaRiego.Api.Contracts;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;

namespace SistemaRiego.Api.Tests;

public sealed class UsersControllerPermissionsTests
{
    [Fact]
    public async Task GetPermissions_MarksRoleAndOverrideSources()
    {
        var (controller, db, user, _) = await Seed();
        var result = await controller.Permissions(user.Id, default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyCollection<EffectivePermissionResponse>>(ok.Value);

        var fromRole = items.Single(x => x.Code == PermissionCodes.IrrigationOperate);
        Assert.True(fromRole.GrantedByRole);
        Assert.Null(fromRole.OverrideIsGranted);
        Assert.True(fromRole.EffectiveGranted);
    }

    [Fact]
    public async Task UpdatePermissions_GrantOverride_AddsPermissionOutsideRole_AndRevokesSessions()
    {
        var (controller, db, user, actorId) = await Seed();
        var activeSession = new Session { UserId = user.Id, RefreshTokenHash = "hash", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        db.Sessions.Add(activeSession);
        await db.SaveChangesAsync();

        var request = new UpdateUserPermissionsRequest([new(PermissionCodes.DeviceCatalogsDelete, true)]);
        var result = await controller.UpdatePermissions(user.Id, request, default);

        Assert.IsType<NoContentResult>(result);
        Assert.True(await db.UserPermissions.AnyAsync(x => x.UserId == user.Id && x.IsGranted));
        Assert.True((await db.Sessions.SingleAsync(x => x.Id == activeSession.Id)).RevokedAtUtc != null);
        Assert.Contains(await db.AccessAudits.ToListAsync(), x => x.EventType == "USER_PERMISSIONS_CHANGED");
    }

    [Fact]
    public async Task UpdatePermissions_UnknownCode_ReturnsBadRequest()
    {
        var (controller, _, user, _) = await Seed();
        var request = new UpdateUserPermissionsRequest([new("codigo.inexistente", true)]);
        var result = await controller.UpdatePermissions(user.Id, request, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static async Task<(UsersController Controller, AppDbContext Db, User User, Guid ActorId)> Seed()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection(); services.AddSingleton(db);
        services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<User>>();

        var role = new Role { Name = RoleNames.Operator, NormalizedName = "OPERADOR" };
        var irrigatePermission = new Permission { Code = PermissionCodes.IrrigationOperate, Description = "Operar riego" };
        var deletePermission = new Permission { Code = PermissionCodes.DeviceCatalogsDelete, Description = "Eliminar catalogos" };
        db.Roles.Add(role); db.Permissions.AddRange(irrigatePermission, deletePermission);
        await db.SaveChangesAsync();
        role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = irrigatePermission.Id });
        var user = new User { Email = "op@correo.gt", UserName = "op@correo.gt", FullName = "Operador" };
        await userManager.CreateAsync(user, "Segura123!");
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        var controller = new UsersController(db, null!, null, userManager);
        var actorId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "tests")) } };
        return (controller, db, user, actorId);
    }
}
```

**Step 3: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter UsersControllerPermissionsTests`
Expected: FAIL — no compila (`controller.Permissions`, `UpdatePermissions`, y el constructor con `UserManager<User>` no existen todavía).

**Step 4: Extender `UsersController`**

En `backend/SistemaRiego.Api/Controllers/UsersController.cs:12-13`, cambiar:

```csharp
[ApiController, Route("api/users"), Authorize(Policy = Policies.Administrator)]
public sealed class UsersController(AppDbContext db, IAuthService auth, ITotpService? totp = null) : ControllerBase
```

por:

```csharp
[ApiController, Route("api/users"), Authorize(Policy = Policies.Administrator)]
public sealed class UsersController(AppDbContext db, IAuthService auth, ITotpService? totp, UserManager<User> userManager) : ControllerBase
```

(el parámetro `totp` deja de tener default `= null` porque ya no es el último parámetro; los call sites reales los resuelve el contenedor DI — no hay instanciaciones manuales en producción).

Añadir `using Microsoft.AspNetCore.Identity;` al tope del archivo si no está.

Después del método `Roles` (línea 37-51), agregar los dos nuevos endpoints:

```csharp
    [HttpGet("{id:guid}/permissions")]
    public async Task<ActionResult<IReadOnlyCollection<EffectivePermissionResponse>>> Permissions(Guid id, CancellationToken ct)
    {
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.RolePermissions).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        var fromRoles = user.UserRoles.SelectMany(x => x.Role.RolePermissions.Select(y => y.Permission.Code)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var overrides = await db.UserPermissions.Include(x => x.Permission).Where(x => x.UserId == id)
            .ToDictionaryAsync(x => x.Permission.Code, x => x.IsGranted, StringComparer.OrdinalIgnoreCase, ct);
        var all = await db.Permissions.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
        return Ok(all.Select(p =>
        {
            var byRole = fromRoles.Contains(p.Code);
            var hasOverride = overrides.TryGetValue(p.Code, out var overrideValue);
            return new EffectivePermissionResponse(p.Code, p.Description, byRole, hasOverride ? overrideValue : null, hasOverride ? overrideValue : byRole);
        }).ToArray());
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdateUserPermissionsRequest request, CancellationToken ct)
    {
        if (!await VerifyTotp(ct)) return StatusCode(StatusCodes.Status403Forbidden, new { message = TotpError });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return NotFound();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();

        var codes = request.Overrides.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Length) return BadRequest(new { message = "La lista contiene permisos desconocidos." });

        db.UserPermissions.RemoveRange(await db.UserPermissions.Where(x => x.UserId == id).ToListAsync(ct));
        foreach (var o in request.Overrides)
        {
            var permission = permissions.Single(x => string.Equals(x.Code, o.Code, StringComparison.OrdinalIgnoreCase));
            db.UserPermissions.Add(new UserPermission { UserId = id, PermissionId = permission.Id, IsGranted = o.IsGranted, GrantedByUserId = actorId });
        }

        foreach (var session in await db.Sessions.Where(x => x.UserId == id && x.RevokedAtUtc == null).ToListAsync(ct)) session.RevokedAtUtc = DateTime.UtcNow;
        await userManager.UpdateSecurityStampAsync(user);

        db.AccessAudits.Add(new AccessAudit { UserId = id, EventType = "USER_PERMISSIONS_CHANGED", Detail = $"Overrides: {string.Join(", ", request.Overrides.Select(x => $"{x.Code}={x.IsGranted}"))}" });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
```

**Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter UsersControllerPermissionsTests`
Expected: PASS (3 tests)

**Step 6: Correr toda la suite**

Run: `dotnet test backend/SistemaRiego.Api.Tests`
Expected: PASS. Prestar atención a cualquier otro sitio que instancie `UsersController` directamente (no debería haber ninguno fuera de este archivo nuevo, según el grep hecho al planear).

**Step 7: Commit**

```bash
git add backend/SistemaRiego.Api/Contracts/UserPermissionContracts.cs backend/SistemaRiego.Api/Controllers/UsersController.cs backend/SistemaRiego.Api.Tests/UsersControllerPermissionsTests.cs
git commit -m "feat: endpoints GET/PUT api/users/{id}/permissions con TOTP y auditoria"
```

---

## Fase 7: Frontend — gestión de permisos por usuario

**Files:**
- Modify: `frontend/src/week2Api.ts`
- Modify: `frontend/src/Sprint2Administration.tsx`

**Step 1: Tipos y llamadas API**

En `frontend/src/week2Api.ts:6-7`, agregar junto a las interfaces existentes:

```typescript
export interface EffectivePermission { code: string; description: string; grantedByRole: boolean; overrideIsGranted: boolean | null; effectiveGranted: boolean }
```

En el objeto `week2Api` (después de `setRolePermissions`, línea 55), agregar:

```typescript
  userPermissions: (token: string, id: string) => request<EffectivePermission[]>(`/users/${id}/permissions`, {}, token),
  setUserPermissions: (token: string, id: string, overrides: { code: string; isGranted: boolean }[], totp: string) => request<void>(`/users/${id}/permissions`, { method: 'PUT', ...json({ overrides }) }, token, totp),
```

**Step 2: UI en `UserSecurityManager`**

En `frontend/src/Sprint2Administration.tsx`, la tabla de usuarios (línea 40) agrega una columna de acción "Permisos" que abre un panel expandible por fila. Añadir estado y handler junto a los existentes (línea 31-32):

```typescript
  const [expandedUser,setExpandedUser]=useState<string>(),[userPermissions,setUserPermissions]=useState<EffectivePermission[]>([])
  const openPermissions=async(u:UserSummary)=>{if(expandedUser===u.id){setExpandedUser(undefined);return}setExpandedUser(u.id);setUserPermissions(await week2Api.userPermissions(session.accessToken,u.id))}
  const toggleUserPermission=async(userId:string,p:EffectivePermission)=>{const next=p.effectiveGranted?false:true;const totp=code();if(!totp)return;await week2Api.setUserPermissions(session.accessToken,userId,[{code:p.code,isGranted:next}],totp);notify('Permiso de usuario actualizado; sus sesiones activas se cerraron.');setUserPermissions(await week2Api.userPermissions(session.accessToken,userId))}
```

Recordar importar el tipo `EffectivePermission` en la línea 4 junto a los demás imports de `./week2Api`.

Agregar la celda de acción en la fila de usuario (línea 40, dentro de `<td>` de acciones, junto al botón "Restablecer contraseña"):

```typescript
<button onClick={()=>openPermissions(u).catch(x=>notify(String(x)))}>{expandedUser===u.id?'Ocultar permisos':'Permisos'}</button>
```

Y justo después de la fila `<tr>` de cada usuario (usando fragment con key derivado), renderizar condicionalmente el panel expandido:

```typescript
{expandedUser===u.id&&<tr key={u.id+'-perms'}><td colSpan={5}><div className="s2-table"><table><thead><tr><th>Permiso</th><th>Origen</th><th>Efectivo</th><th/></tr></thead><tbody>{userPermissions.map(p=><tr key={p.code}><td><b>{p.code}</b><small>{p.description}</small></td><td>{p.overrideIsGranted!==null?(p.overrideIsGranted?'Override: concedido':'Override: revocado'):p.grantedByRole?'Por rol':'—'}</td><td><span className={`s2-status ${p.effectiveGranted?'on':''}`}>{p.effectiveGranted?'Sí':'No'}</span></td><td><button onClick={()=>toggleUserPermission(u.id,p).catch(x=>notify(String(x)))}>{p.effectiveGranted?'Revocar':'Conceder'}</button></td></tr>)}</tbody></table></div></td></tr>}
```

**Step 3: Verificación manual (no hay suite de tests de frontend para este componente)**

Run: `cd frontend && npm run build`
Expected: build sin errores de TypeScript.

Levantar la app (`Iniciar Sistema.ps1` o el flujo habitual del proyecto) y en el navegador:
1. Ir a Usuarios → click "Permisos" en un usuario Operador → confirmar que `riego.operar` aparece como "Por rol" / "Sí".
2. Conceder `dispositivos.catalogos.eliminar` con el código TOTP → confirmar que pasa a "Override: concedido" / "Sí" y que la notificación indica cierre de sesiones.
3. Volver a revocarlo → confirmar que desaparece el override y vuelve a "No" (ya que Operador no lo tiene por rol).

**Step 4: Commit**

```bash
git add frontend/src/week2Api.ts frontend/src/Sprint2Administration.tsx
git commit -m "feat: UI de gestion de permisos granulares por usuario"
```

---

## Fase 8: Piloto — migrar `DeviceCatalogsController` de rol a permiso

Objetivo: demostrar el flujo completo end-to-end (política de permiso protegiendo un endpoint real) y dejar la receta lista para el resto de controllers.

**Files:**
- Modify: `backend/SistemaRiego.Api/Controllers/DeviceCatalogsController.cs`
- Test: crear `backend/SistemaRiego.Api.Tests/DeviceCatalogsAuthorizationTests.cs`

**Step 1: Escribir el test que documenta el contrato de autorización esperado**

Dado que este proyecto no prueba el pipeline `[Authorize]` vía HTTP (ver convención al inicio del plan), este test verifica el **atributo declarado** en cada acción por reflexión — suficiente para detectar que el controller quedó protegido por el permiso correcto y no por accidente sin protección:

Crear `backend/SistemaRiego.Api.Tests/DeviceCatalogsAuthorizationTests.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using SistemaRiego.Api.Controllers;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class DeviceCatalogsAuthorizationTests
{
    [Theory]
    [InlineData(nameof(DeviceCatalogsController.Brands), PermissionPolicies.DeviceCatalogsRead)]
    [InlineData(nameof(DeviceCatalogsController.CreateBrand), PermissionPolicies.DeviceCatalogsManage)]
    [InlineData(nameof(DeviceCatalogsController.UpdateBrand), PermissionPolicies.DeviceCatalogsManage)]
    [InlineData(nameof(DeviceCatalogsController.DeleteBrand), PermissionPolicies.DeviceCatalogsDelete)]
    [InlineData(nameof(DeviceCatalogsController.Models), PermissionPolicies.DeviceCatalogsRead)]
    [InlineData(nameof(DeviceCatalogsController.CreateModel), PermissionPolicies.DeviceCatalogsManage)]
    [InlineData(nameof(DeviceCatalogsController.UpdateModel), PermissionPolicies.DeviceCatalogsManage)]
    [InlineData(nameof(DeviceCatalogsController.DeleteModel), PermissionPolicies.DeviceCatalogsDelete)]
    public void Action_IsProtectedByExpectedPermissionPolicy(string methodName, string expectedPolicy)
    {
        var method = typeof(DeviceCatalogsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().SingleOrDefault();
        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute!.Policy);
    }
}
```

**Step 2: Ejecutar y verificar que falla**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter DeviceCatalogsAuthorizationTests`
Expected: FAIL — hoy las acciones no tienen `[Authorize]` explícito por acción salvo las de escritura/borrado (`Brands`/`Models` de solo lectura heredan la política de clase `Policies.Operator`, no `PermissionPolicies.DeviceCatalogsRead`), y las que sí lo tienen usan `Policies.Technician`/`Policies.Administrator`, no las nuevas políticas de permiso.

**Step 3: Migrar el controller**

En `backend/SistemaRiego.Api/Controllers/DeviceCatalogsController.cs`, reemplazar la línea 10:

```csharp
[ApiController, Route("api/device-catalogs"), Authorize(Policy = Policies.Operator)]
```

por:

```csharp
[ApiController, Route("api/device-catalogs"), Authorize(Policy = PermissionPolicies.DeviceCatalogsRead)]
```

Añadir `[Authorize(Policy = PermissionPolicies.DeviceCatalogsRead)]` explícito en `Brands` (línea 13) y `Models` (línea 44) — son redundantes con la política de clase pero documentan la intención explícita igual que hacen ya las acciones de escritura del resto del código base.

Reemplazar cada atributo de acción:
- Línea 17 `Authorize(Policy = Policies.Technician)` → `Authorize(Policy = PermissionPolicies.DeviceCatalogsManage)` (`CreateBrand`)
- Línea 26 → igual, `PermissionPolicies.DeviceCatalogsManage` (`UpdateBrand`)
- Línea 36 `Authorize(Policy = Policies.Administrator)` → `Authorize(Policy = PermissionPolicies.DeviceCatalogsDelete)` (`DeleteBrand`)
- Línea 48 → `PermissionPolicies.DeviceCatalogsManage` (`CreateModel`)
- Línea 56 → `PermissionPolicies.DeviceCatalogsManage` (`UpdateModel`)
- Línea 65 `Authorize(Policy = Policies.Administrator)` → `Authorize(Policy = PermissionPolicies.DeviceCatalogsDelete)` (`DeleteModel`)

**Step 4: Ejecutar y verificar que pasa**

Run: `dotnet test backend/SistemaRiego.Api.Tests --filter DeviceCatalogsAuthorizationTests`
Expected: PASS (8 casos)

**Step 5: Correr toda la suite y validar manualmente**

Run: `dotnet test backend/SistemaRiego.Api.Tests`
Expected: PASS completo.

Prueba manual con la app corriendo:
1. Login como Operador (rol seedeado con `DeviceCatalogsRead` desde la Fase 2) → `GET /api/device-catalogs/brands` debe responder 200.
2. Mismo usuario Operador intenta `POST /api/device-catalogs/brands` → debe responder 403 (no tiene `DeviceCatalogsManage`).
3. Ir a Usuarios → conceder al Operador el override `dispositivos.catalogos.gestionar` → volver a iniciar sesión (el JWT anterior fue revocado por el bump de `security_stamp`) → repetir el `POST` → debe responder 201/200.
4. Revocar el override → sesión anterior ya inválida por diseño; nuevo login sin el permiso → `POST` vuelve a dar 403.

Esto confirma el ciclo completo: rol da la base, override por usuario la modifica de forma independiente, y el cambio surte efecto de inmediato porque se fuerza cierre de sesión.

**Step 6: Commit**

```bash
git add backend/SistemaRiego.Api/Controllers/DeviceCatalogsController.cs backend/SistemaRiego.Api.Tests/DeviceCatalogsAuthorizationTests.cs
git commit -m "feat: migrar DeviceCatalogsController a autorizacion por permiso (piloto RBAC granular)"
```

---

## Receta para migrar el resto de controllers (seguimiento, no incluido en este plan)

Una vez cerrada la Fase 8, replicar por cada controller restante (~29):
1. Definir sus códigos de permiso en `PermissionCodes.cs` (uno por nivel de acceso que hoy distingue: lectura/gestión/eliminación).
2. Agregar esos códigos a `DbSeeder.cs` con la misma asignación por rol que tienen hoy sus políticas (para no cambiar comportamiento).
3. Agregar las `AddPolicy` correspondientes en `Program.cs` (mismo patrón de la Fase 4/8).
4. Sustituir cada `[Authorize(Policy = Policies.X)]` por la política de permiso equivalente, y escribir el test de reflexión análogo a `DeviceCatalogsAuthorizationTests`.

No requiere tocar `PermissionResolver`, el handler, el JWT ni la UI — esa infraestructura ya quedó genérica desde la Fase 3-7.

---

## Verificación final de todo el plan

Run: `dotnet build "SistemaRiego.sln"` — sin errores ni warnings nuevos.
Run: `dotnet test backend/SistemaRiego.Api.Tests` — 100% verde.
Run: `cd frontend && npm run build` — sin errores de TypeScript.
Revisar manualmente que `dotnet ef database update --project backend/SistemaRiego.Api/SistemaRiego.Api.csproj` aplica la migración `AddUserPermissions` sin conflictos contra la base real (no solo InMemory).
