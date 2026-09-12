using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SistemaRiego.Api.Data;

namespace SistemaRiego.Api.Tests;

public sealed class ApiPipelineTests : IClassFixture<ApiPipelineTests.Factory>
{
    private readonly HttpClient client;

    public ApiPipelineTests(Factory factory) => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task ProtectedEndpoint_UsesTheRealAuthorizationPipeline()
    {
        var response = await client.GetAsync("/api/system/dashboard");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task InvalidLogin_ReturnsSerializedNeutralMessage()
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nadie@correo.gt", password = "Incorrecta123!" });
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Credenciales inválidas o cuenta no disponible", body);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "integration-tests-only-signing-key-with-more-than-32-bytes",
                ["Email:Provider"] = "File",
                ["Seed:IncludeDemoData"] = "false"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<IHostedService>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase($"api-pipeline-{Guid.NewGuid()}"));
            });
        }
    }
}
