using System.Security.Cryptography;
using System.Text;
using CoreVar.CommandLineInterface.Registry;
using CoreVar.CommandLineInterface.Publishing;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var options = new RegistryOptions();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<FileRegistryStore>();
builder.Services.AddHealthChecks();
var app = builder.Build();

if (options.TrustForwardedHeaders)
{
    var forwardedHeaders = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
    };
    forwardedHeaders.KnownIPNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeaders);
}
if (!string.IsNullOrEmpty(options.PathBase)) app.UsePathBase(options.PathBase);

app.MapHealthChecks("/healthz");
app.MapGet("/v1/{tenant}/products/{product}/catalog.json", async (string tenant, string product, FileRegistryStore store, CancellationToken token) =>
    await store.GetReleaseCatalogAsync(tenant, product, token) is { } catalog ? Results.Json(catalog) : Results.NotFound());
app.MapGet("/v1/{tenant}/modules/catalog.json", async (string tenant, FileRegistryStore store, CancellationToken token) =>
    await store.GetModuleCatalogAsync(tenant, token) is { } catalog ? Results.Json(catalog) : Results.NotFound());
app.MapGet("/v1/{tenant}/products/{product}/install.ps1", (HttpRequest request, string tenant, string product, string? channel) =>
    Results.Text(InstallerScriptGenerator.PowerShell(product,
        PublicUri(request, options, $"/v1/{tenant}/products/{product}/catalog.json"), channel ?? "stable"), "text/plain"));
app.MapGet("/v1/{tenant}/products/{product}/install.sh", (HttpRequest request, string tenant, string product, string? channel) =>
    Results.Text(InstallerScriptGenerator.Shell(product,
        PublicUri(request, options, $"/v1/{tenant}/products/{product}/catalog.json"), channel ?? "stable"), "text/x-shellscript"));

app.MapPut("/v1/{tenant}/products/{product}/releases/{version}/{rid}", async (HttpRequest request, string tenant, string product,
    string version, string rid, string? channel, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = AuthorizePublish(request, tenant, options); if (unauthorized is not null) return unauthorized;
    channel ??= "stable";
    var uri = PublicUri(request, options, $"/v1/{tenant}/blobs/products/{product}/{version}/{rid}.zip");
    return Results.Json(await store.PublishReleaseAsync(tenant, product, version, rid, channel, request.Body, uri, token));
});

app.MapPut("/v1/{tenant}/modules/{id}/releases/{version}", async (HttpRequest request, string tenant, string id,
    string version, string? channel, string? description, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = AuthorizePublish(request, tenant, options); if (unauthorized is not null) return unauthorized;
    channel ??= "stable";
    var uri = PublicUri(request, options, $"/v1/{tenant}/blobs/modules/{id}/{version}/module.zip");
    return Results.Json(await store.PublishModuleAsync(tenant, id, version, channel, description ?? string.Empty, request.Body, uri, token));
});

app.MapPost("/v1/{tenant}/products/{product}/channels/{channel}", async (HttpRequest request, string tenant, string product,
    string channel, string version, int? percentage, string? fallbackVersion, string? seed, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = AuthorizePublish(request, tenant, options); if (unauthorized is not null) return unauthorized;
    await store.PromoteAsync(tenant, product, channel, version, percentage ?? 100, fallbackVersion, seed ?? version, token);
    return Results.NoContent();
});

app.MapPost("/v1/{tenant}/products/{product}/revocations", async (HttpRequest request, string tenant, string product,
    string? version, string? sha256, string? reason, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = AuthorizePublish(request, tenant, options); if (unauthorized is not null) return unauthorized;
    await store.RevokeAsync(tenant, product, version, sha256, reason ?? "Revoked by publisher", token);
    return Results.NoContent();
});

app.MapGet("/v1/{tenant}/blobs/{**path}", (string tenant, string path, FileRegistryStore store) =>
{
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var file = store.GetBlobPath(tenant, segments);
    return File.Exists(file) ? Results.File(file, "application/octet-stream", enableRangeProcessing: true) : Results.NotFound();
});

app.Run();

static IResult? AuthorizePublish(HttpRequest request, string tenant, RegistryOptions options)
{
    if (options.AllowAnonymousPublish) return null;
    if (!options.TenantKeys.TryGetValue(tenant, out var expected)) return Results.Unauthorized();
    var supplied = request.Headers.Authorization.ToString();
    if (supplied.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) supplied = supplied[7..];
    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected)) ? null : Results.Unauthorized();
}

static Uri PublicUri(HttpRequest request, RegistryOptions options, string path)
{
    if (options.PublicBaseUri is not null) return new Uri(options.PublicBaseUri, path.TrimStart('/'));
    return new Uri($"{request.Scheme}://{request.Host}{request.PathBase}{path}");
}

public partial class Program;
