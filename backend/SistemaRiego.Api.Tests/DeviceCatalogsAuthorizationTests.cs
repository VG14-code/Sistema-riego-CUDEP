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
