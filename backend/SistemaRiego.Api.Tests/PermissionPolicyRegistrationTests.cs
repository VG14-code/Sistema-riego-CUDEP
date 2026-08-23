using System.Reflection;
using SistemaRiego.Api.Models;

namespace SistemaRiego.Api.Tests;

public sealed class PermissionPolicyRegistrationTests
{
    [Fact]
    public void EveryPermissionPolicy_HasAMatchingPermissionCode()
    {
        // Program.cs registra una AddPolicy por cada campo de PermissionPolicies
        // buscando un campo del mismo nombre en PermissionCodes. Si se agrega una
        // politica sin su codigo homonimo, la app falla al arrancar; este test lo
        // detecta en build/test en vez de en runtime.
        var policyFields = typeof(PermissionPolicies).GetFields(BindingFlags.Public | BindingFlags.Static);
        Assert.NotEmpty(policyFields);
        foreach (var field in policyFields)
            Assert.True(typeof(PermissionCodes).GetField(field.Name) is not null, $"Falta PermissionCodes.{field.Name} para la politica {field.Name}.");
    }
}
