using Microsoft.AspNetCore.Authentication.JwtBearer;
using CoreVar.CommandLineInterface.Registry;
using CoreVar.CommandLineInterface.Publishing;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var options = new RegistryOptions();
builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = options.MaxUploadBytes);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<FileRegistryStore>();
builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(options.OidcAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
    {
        jwt.Authority = options.OidcAuthority;
        jwt.Audience = options.OidcAudience;
        jwt.RequireHttpsMetadata = options.OidcRequireHttpsMetadata;
        jwt.MapInboundClaims = false;
    });
    builder.Services.AddAuthorization();
}
var app = builder.Build();

app.Use(async (context, next) =>
{
    if (HttpMethods.IsPut(context.Request.Method) && context.Request.ContentLength > options.MaxUploadBytes)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }
    try { await next(); }
    catch (RegistryUploadTooLargeException) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
    }
});

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
if (!string.IsNullOrWhiteSpace(options.OidcAuthority))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapHealthChecks("/healthz");
app.MapGet("/v1/{tenant}/products/{product}/catalog.json", async (HttpRequest request, string tenant, string product, FileRegistryStore store, CancellationToken token) =>
    ReadDenied(request, tenant, "catalog.read", $"product:{product}", options) ?? (await store.GetReleaseCatalogAsync(tenant, product, token) is { } catalog ? Results.Json(catalog) : Results.NotFound()));
app.MapGet("/v1/{tenant}/modules/catalog.json", async (HttpRequest request, string tenant, FileRegistryStore store, CancellationToken token) =>
    ReadDenied(request, tenant, "catalog.read", "module:*", options) ?? (await store.GetModuleCatalogAsync(tenant, token) is { } catalog ? Results.Json(catalog) : Results.NotFound()));
app.MapGet("/v1/{tenant}/products/{product}/install.ps1", (HttpRequest request, string tenant, string product, string? channel) =>
    ReadDenied(request, tenant, "catalog.read", $"product:{product}", options) ?? Results.Text(InstallerScriptGenerator.PowerShell(product,
        PublicUri(request, options, $"/v1/{tenant}/products/{product}/catalog.json"), channel ?? "stable"), "text/plain"));
app.MapGet("/v1/{tenant}/products/{product}/install.sh", (HttpRequest request, string tenant, string product, string? channel) =>
    ReadDenied(request, tenant, "catalog.read", $"product:{product}", options) ?? Results.Text(InstallerScriptGenerator.Shell(product,
        PublicUri(request, options, $"/v1/{tenant}/products/{product}/catalog.json"), channel ?? "stable"), "text/x-shellscript"));

app.MapPut("/v1/{tenant}/products/{product}/releases/{version}/{rid}", async (HttpRequest request, string tenant, string product,
    string version, string rid, string? channel, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = RegistryAccess.Authorize(request, tenant, "release.publish", $"product:{product}", options); if (unauthorized is not null) return unauthorized;
    channel ??= "stable";
    var uri = PublicUri(request, options, $"/v1/{tenant}/blobs/products/{product}/{version}/{rid}.zip");
    var result = await store.PublishReleaseAsync(tenant, product, version, rid, channel, request.Body, uri, token);
    Audit(app, request, tenant, "release.publish", $"product:{product}", version);
    return Results.Json(result);
});

app.MapPut("/v1/{tenant}/modules/{id}/releases/{version}", async (HttpRequest request, string tenant, string id,
    string version, string? channel, string? description, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = RegistryAccess.Authorize(request, tenant, "module.publish", $"module:{id}", options); if (unauthorized is not null) return unauthorized;
    channel ??= "stable";
    var uri = PublicUri(request, options, $"/v1/{tenant}/blobs/modules/{id}/{version}/module.zip");
    var result = await store.PublishModuleAsync(tenant, id, version, channel, description ?? string.Empty, request.Body, uri, token);
    Audit(app, request, tenant, "module.publish", $"module:{id}", version);
    return Results.Json(result);
});

app.MapPost("/v1/{tenant}/products/{product}/channels/{channel}", async (HttpRequest request, string tenant, string product,
    string channel, string version, int? percentage, string? fallbackVersion, string? seed, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = RegistryAccess.Authorize(request, tenant, "release.promote", $"product:{product}", options); if (unauthorized is not null) return unauthorized;
    await store.PromoteAsync(tenant, product, channel, version, percentage ?? 100, fallbackVersion, seed ?? version, token);
    Audit(app, request, tenant, "release.promote", $"product:{product}", version);
    return Results.NoContent();
});

app.MapPost("/v1/{tenant}/products/{product}/revocations", async (HttpRequest request, string tenant, string product,
    string? version, string? sha256, string? reason, FileRegistryStore store, CancellationToken token) =>
{
    var unauthorized = RegistryAccess.Authorize(request, tenant, "release.revoke", $"product:{product}", options); if (unauthorized is not null) return unauthorized;
    await store.RevokeAsync(tenant, product, version, sha256, reason ?? "Revoked by publisher", token);
    Audit(app, request, tenant, "release.revoke", $"product:{product}", version ?? sha256 ?? "unspecified");
    return Results.NoContent();
});

app.MapGet("/v1/{tenant}/blobs/{**path}", (HttpRequest request, string tenant, string path, FileRegistryStore store) =>
{
    var denied = ReadDenied(request, tenant, "artifact.read", path.StartsWith("modules/", StringComparison.OrdinalIgnoreCase) ? "module:*" : "product:*", options);
    if (denied is not null) return denied;
    var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    var file = store.GetBlobPath(tenant, segments);
    return File.Exists(file) ? Results.File(file, "application/octet-stream", enableRangeProcessing: true) : Results.NotFound();
});

app.Run();

static IResult? ReadDenied(HttpRequest request, string tenant, string permission, string resource, RegistryOptions options) =>
    options.RequireAuthenticatedReads ? RegistryAccess.Authorize(request, tenant, permission, resource, options) : null;

static void Audit(WebApplication app, HttpRequest request, string tenant, string operation, string resource, string version) =>
    app.Logger.LogInformation("CLI registry mutation. Subject={Subject}, Tenant={Tenant}, Operation={Operation}, Resource={Resource}, Version={Version}, AuthenticationType={AuthenticationType}, CorrelationId={CorrelationId}",
        request.HttpContext.User.FindFirst("sub")?.Value ?? "api-key", tenant, operation, resource, version,
        request.HttpContext.User.Identity?.AuthenticationType ?? "api-key", request.HttpContext.TraceIdentifier);

static Uri PublicUri(HttpRequest request, RegistryOptions options, string path)
{
    if (options.PublicBaseUri is not null) return new Uri(options.PublicBaseUri, path.TrimStart('/'));
    return new Uri($"{request.Scheme}://{request.Host}{request.PathBase}{path}");
}

public partial class Program;
