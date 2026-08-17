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

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));
builder.Services.AddControllers();
builder.Services.AddRateLimiter(options => { options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; options.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; limiter.AutoReplenishment = true; }); });
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ITelemetryIngestionService, TelemetryIngestionService>();
builder.Services.AddScoped<IIrrigationRecommendationCalculator, IrrigationRecommendationCalculator>();
builder.Services.AddSingleton<SpatialGeometryValidator>();
builder.Services.AddScoped<TerritoryIntegrityService>();
builder.Services.AddScoped<IrrigationAckService>();
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
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequiredLength = 8; options.Password.RequireUppercase = true; options.Password.RequireLowercase = true; options.Password.RequireDigit = true; options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
}).AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITotpService, TotpService>();


var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("Falta la configuración JWT.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
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
        }
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Administrator, p => p.RequireRole(RoleNames.Administrator));
    o.AddPolicy(Policies.Technician, p => p.RequireRole(RoleNames.Administrator, RoleNames.Technician));
    o.AddPolicy(Policies.Operator, p => p.RequireRole(RoleNames.Administrator, RoleNames.Technician, RoleNames.Operator));
});
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");
await DbSeeder.SeedAsync(app.Services, app.Configuration);
app.Run();
public partial class Program;
