using System;

namespace Jellyfin.Plugin.OAuth.Authentication.Models;

public class OAuthUser
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Picture { get; set; }
    public required string Provider { get; set; }
    public DateTime AuthenticatedAt { get; set; } = DateTime.UtcNow;
}

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public required string TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
    public string? IdToken { get; set; }
}

public class OAuthSessionData
{
    public string? InviteCode { get; set; }
    public string? ReturnUrl { get; set; }
    public DateTime ExpiresAt { get; set; }
}