using System.ComponentModel.DataAnnotations;
using System.Reflection;
using SistemaRiego.Api.Contracts;

namespace SistemaRiego.Api.Tests;

public sealed class ContractValidationMetadataTests
{
    // MVC rechaza con 500 cualquier record cuyo atributo de validación esté en la
    // propiedad ([property: Required]) en vez del parámetro del constructor. Las
    // pruebas que llaman al controlador directamente no pasan por el model binding,
    // así que POST /api/roles falló en la aplicación real con la suite en verde.
    [Fact]
    public void RecordContracts_DeclareValidationOnConstructorParameters()
    {
        var offenders = typeof(SaveRoleRequest).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(SaveRoleRequest).Namespace)
            .SelectMany(type => type.GetConstructors().SelectMany(ctor => ctor.GetParameters())
                .Select(parameter => type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance))
                .Where(property => property?.GetCustomAttributes<ValidationAttribute>().Any() == true)
                .Select(property => $"{type.Name}.{property!.Name}"))
            .Distinct()
            .ToArray();

        Assert.True(offenders.Length == 0, "Validación declarada en la propiedad: " + string.Join(", ", offenders));
    }
}
