using System.Security.Claims;
using CoreVar.CommandLineInterface.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace CommandLineInterface.Registry.Tests;

public sealed class RegistryAccessTests
{
    [Fact]
    public void Matching_oidc_tenant_permission_and_resource_is_allowed()
    {
        var request = Request("acme", "release.publish", "product:tools-*");
        Assert.Null(RegistryAccess.Authorize(request, "acme", "release.publish", "product:tools-cli", new RegistryOptions()));
    }

    [Theory]
    [InlineData("other", "release.publish", "product:tools-*", "acme", "release.publish", "product:tools-cli")]
    [InlineData("acme", "artifact.read", "product:tools-*", "acme", "release.publish", "product:tools-cli")]
    [InlineData("acme", "release.publish", "module:*", "acme", "release.publish", "product:tools-cli")]
    public void Oidc_grants_fail_closed(string claimTenant, string claimPermission, string claimResource,
        string tenant, string permission, string resource)
    {
        Assert.IsType<ForbidHttpResult>(RegistryAccess.Authorize(Request(claimTenant, claimPermission, claimResource),
            tenant, permission, resource, new RegistryOptions()));
    }

    [Fact]
    public void Legacy_tenant_key_remains_a_compatibility_path()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer secret";
        var options = new RegistryOptions { TenantKeys = new Dictionary<string, string> { ["acme"] = "secret" } };
        Assert.Null(RegistryAccess.Authorize(context.Request, "acme", "release.publish", "product:any", options));
    }

    [Fact]
    public void Anonymous_request_is_rejected()
    {
        Assert.IsType<UnauthorizedHttpResult>(RegistryAccess.Authorize(new DefaultHttpContext().Request,
            "acme", "release.publish", "product:any", new RegistryOptions()));
    }

    private static HttpRequest Request(string tenant, string permission, string resource)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("corevar:tenant", tenant),
                new Claim("permission", permission),
                new Claim("corevar:registry:resource", resource)
            ], "test"))
        };
        return context.Request;
    }
}
