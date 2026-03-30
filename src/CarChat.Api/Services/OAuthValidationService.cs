using System.Net.Http.Headers;
using System.Text.Json;

namespace CarChat.Api.Services;

/// <summary>
/// Validates OAuth tokens by calling each provider's userinfo endpoint.
/// </summary>
public class OAuthValidationService(IHttpClientFactory httpClientFactory, ILogger<OAuthValidationService> logger)
    : IOAuthValidationService
{
    public async Task<OAuthUserInfo?> ValidateAsync(string provider, string identityToken, CancellationToken ct = default)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => await ValidateGoogleAsync(identityToken, ct),
            "github" => await ValidateGitHubAsync(identityToken, ct),
            "microsoft" => await ValidateMicrosoftAsync(identityToken, ct),
            _ => null,
        };
    }

    private async Task<OAuthUserInfo?> ValidateGoogleAsync(string token, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(token)}", ct);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var sub = json.GetProperty("sub").GetString();
            var email = json.GetProperty("email").GetString();
            var name = json.TryGetProperty("name", out var n) ? n.GetString() : email;

            if (sub is null || email is null) return null;
            return new OAuthUserInfo(sub, email, name ?? email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google token validation failed");
            return null;
        }
    }

    private async Task<OAuthUserInfo?> ValidateGitHubAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CarChatPlatform/1.0");

            var response = await client.GetAsync("https://api.github.com/user", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var sub = json.GetProperty("id").GetInt64().ToString();
            var login = json.GetProperty("login").GetString() ?? string.Empty;
            var name = json.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null
                ? n.GetString() ?? login
                : login;

            string email;
            if (json.TryGetProperty("email", out var emailProp) && emailProp.ValueKind == JsonValueKind.String)
            {
                email = emailProp.GetString()!;
            }
            else
            {
                email = await GetGitHubPrimaryEmailAsync(client, ct) ?? $"{login}@github.local";
            }

            return new OAuthUserInfo(sub, email, name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitHub token validation failed");
            return null;
        }
    }

    private static async Task<string?> GetGitHubPrimaryEmailAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.GetAsync("https://api.github.com/user/emails", ct);
        if (!response.IsSuccessStatusCode) return null;

        var emails = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        foreach (var item in emails.EnumerateArray())
        {
            if (item.TryGetProperty("primary", out var p) && p.GetBoolean() &&
                item.TryGetProperty("email", out var e))
                return e.GetString();
        }
        return null;
    }

    private async Task<OAuthUserInfo?> ValidateMicrosoftAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var sub = json.GetProperty("id").GetString()!;
            var email = json.TryGetProperty("mail", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()!
                : json.GetProperty("userPrincipalName").GetString()!;
            var name = json.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? email : email;

            return new OAuthUserInfo(sub, email, name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Microsoft token validation failed");
            return null;
        }
    }
}
