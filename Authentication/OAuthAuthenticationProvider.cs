using System;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.OAuth.Authentication.Models;
using Jellyfin.Plugin.OAuth.Authentication.Providers;
using Jellyfin.Plugin.OAuth.Configuration;
using Jellyfin.Plugin.OAuth.Services;
using MediaBrowser.Controller.Authentication;
using Jellyfin.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OAuth.Authentication;

public class OAuthAuthenticationProvider : IAuthenticationProvider
{
    private readonly ILogger<OAuthAuthenticationProvider> _logger;
    private readonly GoogleOAuthProvider _googleProvider;
    private readonly IUserProvisioningService _userProvisioningService;
    private readonly PluginConfiguration _config;

    public string Name => "OAuth";

    public bool IsEnabled => _config.EnableGoogleOAuth || _config.EnableAppleOAuth;

    public OAuthAuthenticationProvider(
        ILogger<OAuthAuthenticationProvider> logger,
        GoogleOAuthProvider googleProvider,
        IUserProvisioningService userProvisioningService,
        PluginConfiguration config)
    {
        _logger = logger;
        _googleProvider = googleProvider;
        _userProvisioningService = userProvisioningService;
        _config = config;
    }

    public Task<ProviderAuthenticationResult> Authenticate(string username, string password)
    {
        throw new NotImplementedException("OAuth authentication is handled through web flow, not username/password");
    }

    public bool HasPassword(User user)
    {
        return false;
    }

    public Task ChangePassword(User user, string newPassword)
    {
        throw new NotSupportedException("OAuth users cannot change passwords through Jellyfin");
    }

    public async Task<User> AuthenticateWithTokenAsync(string provider, string accessToken, string? inviteCode = null)
    {
        try
        {
            OAuthUser oauthUser = provider.ToLower() switch
            {
                "google" => await _googleProvider.GetUserInfoAsync(accessToken),
                _ => throw new ArgumentException($"Unsupported OAuth provider: {provider}")
            };

            var user = await _userProvisioningService.ProvisionUserAsync(oauthUser, inviteCode);
            
            _logger.LogInformation("Successfully authenticated user {Email} via {Provider}", 
                oauthUser.Email, provider);
            
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth authentication failed for provider {Provider}", provider);
            throw;
        }
    }
}