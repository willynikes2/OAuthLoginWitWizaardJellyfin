using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Plugin.OAuth.Authentication.Models;
using Jellyfin.Plugin.OAuth.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OAuth.Authentication.Providers;

public class GoogleOAuthProvider
{
    private readonly ILogger<GoogleOAuthProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly PluginConfiguration _config;

    private const string AuthorizationUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string UserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo";

    public GoogleOAuthProvider(ILogger<GoogleOAuthProvider> logger, HttpClient httpClient, PluginConfiguration config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config;
    }

    public Task<string> GetAuthorizationUrlAsync(string state)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _config.GoogleSettings.ClientId,
            ["redirect_uri"] = GetRedirectUri(),
            ["scope"] = _config.GoogleSettings.Scopes,
            ["response_type"] = "code",
            ["state"] = state,
            ["access_type"] = "offline"
        };

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));
        return Task.FromResult($"{AuthorizationUrl}?{queryString}");
    }

    public async Task<TokenResponse> ExchangeCodeForTokenAsync(string code)
    {
        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = _config.GoogleSettings.ClientId,
            ["client_secret"] = _config.GoogleSettings.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = GetRedirectUri()
        };

        var content = new FormUrlEncodedContent(requestData);
        var response = await _httpClient.PostAsync(TokenUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to exchange code for token: {Error}", error);
            throw new HttpRequestException($"Token exchange failed: {response.StatusCode}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

        return new TokenResponse
        {
            AccessToken = tokenData.GetProperty("access_token").GetString()!,
            RefreshToken = tokenData.TryGetProperty("refresh_token", out var refreshToken) ? refreshToken.GetString() : null,
            TokenType = tokenData.GetProperty("token_type").GetString()!,
            ExpiresIn = tokenData.GetProperty("expires_in").GetInt32(),
            Scope = tokenData.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
            IdToken = tokenData.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null
        };
    }

    public async Task<OAuthUser> GetUserInfoAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await _httpClient.GetAsync(UserInfoUrl);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get user info: {Error}", error);
            throw new HttpRequestException($"User info request failed: {response.StatusCode}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        var userData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

        return new OAuthUser
        {
            Id = userData.GetProperty("id").GetString()!,
            Email = userData.GetProperty("email").GetString()!,
            Name = userData.TryGetProperty("name", out var name) ? name.GetString() : null,
            GivenName = userData.TryGetProperty("given_name", out var givenName) ? givenName.GetString() : null,
            FamilyName = userData.TryGetProperty("family_name", out var familyName) ? familyName.GetString() : null,
            Picture = userData.TryGetProperty("picture", out var picture) ? picture.GetString() : null,
            Provider = "Google"
        };
    }

    private string GetRedirectUri()
    {
        return _config.GoogleSettings.RedirectUri;
    }
}