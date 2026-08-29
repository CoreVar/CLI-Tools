using System.Net.Http.Headers;
using System.Text.Json;

namespace CoreVar.CommandLineInterface.Publishing;

public sealed class RegistryPublisher(HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient();

    public ValueTask<PublishResult> PublishCliAsync(Uri endpoint, string tenant, string product, string version,
        string runtimeIdentifier, string artifact, string channel = "stable", string? token = null,
        CancellationToken cancellationToken = default) => PublishAsync(
            Resolve(endpoint, $"v1/{Escape(tenant)}/products/{Escape(product)}/releases/{Escape(version)}/{Escape(runtimeIdentifier)}?channel={Escape(channel)}"),
            artifact, token, cancellationToken);

    public ValueTask<PublishResult> PublishModuleAsync(Uri endpoint, string tenant, string id, string version,
        string artifact, string channel = "stable", string? description = null, string? token = null,
        CancellationToken cancellationToken = default) => PublishAsync(
            Resolve(endpoint, $"v1/{Escape(tenant)}/modules/{Escape(id)}/releases/{Escape(version)}?channel={Escape(channel)}&description={Escape(description ?? string.Empty)}"),
            artifact, token, cancellationToken);

    public ValueTask PromoteAsync(Uri endpoint, string tenant, string product, string channel, string version,
        int percentage = 100, string? fallbackVersion = null, string? token = null, CancellationToken cancellationToken = default) =>
        PostAsync(Resolve(endpoint, $"v1/{Escape(tenant)}/products/{Escape(product)}/channels/{Escape(channel)}?version={Escape(version)}&percentage={percentage}&fallbackVersion={Escape(fallbackVersion ?? string.Empty)}"), token, cancellationToken);

    public ValueTask RevokeAsync(Uri endpoint, string tenant, string product, string? version, string? sha256,
        string reason, string? token = null, CancellationToken cancellationToken = default) =>
        PostAsync(Resolve(endpoint, $"v1/{Escape(tenant)}/products/{Escape(product)}/revocations?version={Escape(version ?? string.Empty)}&sha256={Escape(sha256 ?? string.Empty)}&reason={Escape(reason)}"), token, cancellationToken);

    private async ValueTask<PublishResult> PublishAsync(Uri uri, string artifact, string? token, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(artifact);
        using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = new StreamContent(stream) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Registry returned {(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        return new PublishResult(root.TryGetProperty("uri", out var publishedUri) ? publishedUri.GetString() : null,
            root.TryGetProperty("sha256", out var sha) ? sha.GetString() : null);
    }

    private async ValueTask PostAsync(Uri uri, string? token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Registry returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static Uri Resolve(Uri endpoint, string relative) => new(new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/"), relative);
}

public sealed record PublishResult(string? Uri, string? Sha256);
