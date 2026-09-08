using System.Security.Cryptography;
using System.Text;

namespace CoreVar.CommandLineInterface.Registry;

internal static class RegistryAccess
{
    public static IResult? Authorize(HttpRequest request, string tenant, string permission, string resource, RegistryOptions options)
    {
        if (options.AllowAnonymousPublish && permission.EndsWith(".publish", StringComparison.Ordinal)) return null;
        if (MatchesLegacyKey(request, tenant, options)) return null;

        var user = request.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
        if (!user.Claims.Any(claim => claim.Type == "corevar:tenant" && claim.Value.Equals(tenant, StringComparison.OrdinalIgnoreCase)) &&
            !user.Claims.Any(claim => claim.Type == "corevar:tenant_id" && claim.Value.Equals(tenant, StringComparison.OrdinalIgnoreCase)))
            return Results.Forbid();
        if (!user.Claims.Any(claim => claim.Type == "permission" &&
            (claim.Value == "cli-registry.admin" || claim.Value.Equals(permission, StringComparison.Ordinal))))
            return Results.Forbid();
        var grants = user.Claims.Where(claim => claim.Type == "corevar:registry:resource").Select(claim => claim.Value).ToArray();
        return grants.Any(grant => MatchesResource(grant, resource)) ? null : Results.Forbid();
    }

    private static bool MatchesLegacyKey(HttpRequest request, string tenant, RegistryOptions options)
    {
        if (!options.TenantKeys.TryGetValue(tenant, out var expected)) return false;
        var supplied = request.Headers.Authorization.ToString();
        if (supplied.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) supplied = supplied[7..];
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
    }

    private static bool MatchesResource(string grant, string resource) => grant == "*" ||
        grant.Equals(resource, StringComparison.OrdinalIgnoreCase) ||
        (grant.EndsWith('*') && resource.StartsWith(grant[..^1], StringComparison.OrdinalIgnoreCase));
}
