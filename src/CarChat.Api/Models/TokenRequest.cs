namespace CarChat.Api.Models;

public record TokenRequest(
    string Provider,       // "google" | "github" | "microsoft"
    string IdentityToken   // OAuth identity token / access token from provider
);

public record TokenResponse(string Token, DateTime ExpiresAt);
