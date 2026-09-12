using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SistemaRiego.Api.Data;
using SistemaRiego.Api.Hubs;
using SistemaRiego.Api.Middleware;
using SistemaRiego.Api.Models;
using SistemaRiego.Api.Services;
using Serilog;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options => { options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; limiter.AutoReplenishment = true; }); });
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ITelemetryIngestionService, TelemetryIngestionService>();
builder.Services.AddScoped<IIrrigationRecommendationCalculator, IrrigationRecommendationCalculator>();
builder.Services.AddSingleton<SpatialGeometryValidator>();
builder.Services.AddScoped<TerritoryIntegrityService>();
builder.Services.AddScoped<IrrigationAckService>();
builder.Services.AddScoped<IRemoteConfigurationDispatcher, RemoteConfigurationDispatcher>();
builder.Services.AddHostedService<RemoteConfigurationWorker>();
builder.Services.AddScoped<PumpAckService>();
builder.Services.AddScoped<IPumpCommandService, PumpCommandService>();
builder.Services.AddScoped<ISprint4TelemetryService, Sprint4TelemetryService>();
builder.Services.AddScoped<IWaterCapacityService, WaterCapacityService>();
builder.Services.AddScoped<IConsumptionCalculator, ConsumptionCalculator>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<Sprint6ReportService>();
builder.Services.AddScoped<TableExportService>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddHostedService<AuditRetentionService>();
builder.Services.AddScoped<IAlertEscalationProcessor, AlertEscalationProcessor>();
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.SectionName));
builder.Services.AddHostedService<AlertEscalationWorker>();
builder.Services.AddScoped<IIrrigationCommandService, IrrigationCommandService>();
builder.Services.AddScoped<IAutomationEngine, AutomationEngine>();
builder.Services.AddHostedService<AutomationSchedulerWorker>();
builder.Services.AddHostedService<IrrigationWatchdogWorker>();
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.Configure<IoTHealthOptions>(builder.Configuration.GetSection(IoTHealthOptions.SectionName));
builder.Services.Configure<CropStageTransitionOptions>(builder.Configuration.GetSection(CropStageTransitionOptions.SectionName));
builder.Services.AddHostedService<IoTHealthWorker>();
builder.Services.AddHostedService<CropStageTransitionWorker>();
if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<EmbeddedMqttBroker>();
builder.Services.AddSingleton<MqttWorker>();
builder.Services.AddSingleton<IMqttCommandPublisher>(serviceProvider => serviceProvider.GetRequiredService<MqttWorker>());
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<MqttWorker>());
builder.Services.AddDbContext<AppDbContext>((services, options) => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)).AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequiredLength = 8; options.Password.RequireUppercase = true; options.Password.RequireLowercase = true; options.Password.RequireDigit = true; options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
}).AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionResolver, PermissionResolver>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IEmailSender>(services =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value;
    return string.Equals(settings.Provider, "Smtp", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(services)
        : ActivatorUtilities.CreateInstance<FileEmailSender>(services);
});
builder.Services.AddScoped<ITotpService, TotpService>();


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("Falta la configuración JWT.");
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ClockSkew = TimeSpan.FromSeconds(30)
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs")) context.Token = accessToken;
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
            var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var stamp = context.Principal?.FindFirst("security_stamp")?.Value;
            if (!Guid.TryParse(id, out var userId) || string.IsNullOrWhiteSpace(stamp))
            {
                context.Fail("Token sin sello de seguridad.");
                return;
            }
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || user.Status != UserStatus.Active || !string.Equals(stamp, await userManager.GetSecurityStampAsync(user), StringComparison.Ordinal))
                context.Fail("La sesión fue invalidada.");
        }
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Administrator, p => p.RequireRole(RoleNames.Administrator));
    o.AddPolicy(Policies.Technician, p => p.RequireRole(RoleNames.Administrator, RoleNames.Technician));
    o.AddPolicy(Policies.Operator, p => p.RequireRole(RoleNames.Administrator, RoleNames.Technician, RoleNames.Operator));
    // Cada campo de PermissionPolicies registra una politica que exige el codigo de
    // PermissionCodes del mismo nombre; mantiene ambas clases sincronizadas por
    // construccion en vez de listar cada AddPolicy a mano.
    foreach (var field in typeof(PermissionPolicies).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var policyName = (string)field.GetValue(null)!;
        var codeField = typeof(PermissionCodes).GetField(field.Name) ?? throw new InvalidOperationException($"Falta PermissionCodes.{field.Name} para la politica {field.Name}.");
        var code = (string)codeField.GetValue(null)!;
        o.AddPolicy(policyName, p => p.Requirements.Add(new PermissionRequirement(code)));
    }
});
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Content-Disposition")));  // el navegador necesita leerla para nombrar las descargas

var app = builder.Build();
var activeEmailProvider = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value.Provider;
app.Logger.LogInformation("Proveedor de correo activo: {EmailProvider}", activeEmailProvider);
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<RequiredPasswordChangeMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");
// La muestra sintetica de demostracion solo se siembra en Development, salvo que
// Seed:IncludeDemoData lo indique explicitamente para otro entorno.
var includeDemoData = app.Configuration.GetValue<bool?>("Seed:IncludeDemoData") ?? app.Environment.IsDevelopment();
await DbSeeder.SeedAsync(app.Services, app.Configuration, includeDemoData);
app.Run();
public partial class Program;
