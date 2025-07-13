using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.OAuth.Authentication;
using Jellyfin.Plugin.OAuth.Authentication.Models;
using Jellyfin.Plugin.OAuth.Authentication.Providers;
using Jellyfin.Plugin.OAuth.Configuration;
using Jellyfin.Plugin.OAuth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OAuth.Controllers;

[ApiController]
[Route("oauth")]
public class OAuthController : ControllerBase
{
    private readonly ILogger<OAuthController> _logger;
    private readonly GoogleOAuthProvider? _googleProvider;
    private readonly OAuthAuthenticationProvider? _authProvider;
    private readonly IWizarrIntegrationService? _wizarrService;
    private readonly IMemoryCache _cache;
    private readonly PluginConfiguration _config;

    public OAuthController(
        ILogger<OAuthController> logger,
        IMemoryCache cache,
        PluginConfiguration config,
        GoogleOAuthProvider? googleProvider = null,
        OAuthAuthenticationProvider? authProvider = null,
        IWizarrIntegrationService? wizarrService = null)
    {
        _logger = logger;
        _cache = cache;
        _config = config;
        _googleProvider = googleProvider;
        _authProvider = authProvider;
        _wizarrService = wizarrService;
        
        _logger.LogInformation("OAuth Controller: Initialized with Google Provider: {GoogleProvider}, Auth Provider: {AuthProvider}, Wizarr Service: {WizarrService}", 
            _googleProvider != null, _authProvider != null, _wizarrService != null);
    }

    [HttpGet("{provider}/authorize")]
    public async Task<IActionResult> Authorize(string provider, string? invite = null, string? return_url = null)
    {
        try
        {
            _logger.LogInformation("OAuth Controller: Authorize request for provider {Provider}, invite: {Invite}, return_url: {ReturnUrl}", 
                provider, invite, return_url);

            if (!IsProviderEnabled(provider))
            {
                _logger.LogWarning("OAuth Controller: Provider {Provider} is not enabled", provider);
                return BadRequest($"OAuth provider '{provider}' is not enabled");
            }

            // Validate invite if required
            if (!string.IsNullOrEmpty(invite) && _config.EnableWizarrIntegration && _wizarrService != null)
            {
                var isValidInvite = await _wizarrService.ValidateInviteAsync(invite);
                if (!isValidInvite)
                {
                    _logger.LogWarning("OAuth Controller: Invalid invite code provided: {Invite}", invite);
                    return BadRequest("Invalid or expired invite code");
                }
            }
            else if (_config.RequireValidInvite && _config.EnableWizarrIntegration)
            {
                _logger.LogWarning("OAuth Controller: Valid invite code is required but none provided");
                return BadRequest("Valid invite code is required");
            }

            var state = GenerateState();
            _logger.LogInformation("OAuth Controller: Generated state: {State}", state);
            
            // Store session data if needed
            if (!string.IsNullOrEmpty(invite) || !string.IsNullOrEmpty(return_url))
            {
                await StoreSessionDataAsync(state, invite, return_url);
                _logger.LogInformation("OAuth Controller: Stored session data for state: {State}", state);
            }

            // Get authorization URL based on provider
            string authUrl;
            switch (provider.ToLower())
            {
                case "google":
                    if (_googleProvider == null)
                    {
                        _logger.LogError("OAuth Controller: Google provider is not available");
                        return BadRequest("Google OAuth provider is not properly configured");
                    }
                    authUrl = await _googleProvider.GetAuthorizationUrlAsync(state);
                    break;
                default:
                    _logger.LogError("OAuth Controller: Unsupported provider: {Provider}", provider);
                    return BadRequest($"Unsupported provider: {provider}");
            }

            _logger.LogInformation("OAuth Controller: Redirecting to authorization URL for provider {Provider}", provider);
            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Controller: Error initiating OAuth flow for provider {Provider}", provider);
            return BadRequest("Failed to initiate OAuth flow");
        }
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, string code, string state, string? error = null)
    {
        try
        {
            _logger.LogInformation("OAuth Controller: Callback received for provider {Provider}, state: {State}, error: {Error}", 
                provider, state, error);

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("OAuth Controller: OAuth callback received error: {Error}", error);
                return BadRequest($"OAuth error: {error}");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("OAuth Controller: No authorization code received in callback");
                return BadRequest("No authorization code received");
            }

            if (!IsProviderEnabled(provider))
            {
                _logger.LogWarning("OAuth Controller: Provider {Provider} is not enabled", provider);
                return BadRequest($"OAuth provider '{provider}' is not enabled");
            }

            // Get session data
            var sessionData = await GetSessionDataAsync(state);
            _logger.LogInformation("OAuth Controller: Retrieved session data for state: {State}, has invite: {HasInvite}", 
                state, sessionData?.InviteCode != null);
            
            // Exchange code for token based on provider
            TokenResponse tokenResponse;
            switch (provider.ToLower())
            {
                case "google":
                    if (_googleProvider == null)
                    {
                        _logger.LogError("OAuth Controller: Google provider is not available for callback");
                        return BadRequest("Google OAuth provider is not properly configured");
                    }
                    tokenResponse = await _googleProvider.ExchangeCodeForTokenAsync(code);
                    break;
                default:
                    _logger.LogError("OAuth Controller: Unsupported provider in callback: {Provider}", provider);
                    return BadRequest($"Unsupported provider: {provider}");
            }

            _logger.LogInformation("OAuth Controller: Successfully exchanged code for token");

            // Authenticate user
            if (_authProvider == null)
            {
                _logger.LogError("OAuth Controller: Authentication provider is not available");
                return BadRequest("OAuth authentication provider is not properly configured");
            }

            var user = await _authProvider.AuthenticateWithTokenAsync(provider, tokenResponse.AccessToken, sessionData?.InviteCode);
            _logger.LogInformation("OAuth Controller: Successfully authenticated user: {UserId}", user.Id);

            // Mark invite as used if applicable
            if (sessionData?.InviteCode != null && _config.EnableWizarrIntegration && _wizarrService != null)
            {
                await _wizarrService.MarkInviteAsUsedAsync(sessionData.InviteCode, user.Id);
                _logger.LogInformation("OAuth Controller: Marked invite as used: {Invite}", sessionData.InviteCode);
            }

            // Cleanup session data
            await CleanupSessionDataAsync(state);

            var returnUrl = sessionData?.ReturnUrl ?? "/web/index.html";
            _logger.LogInformation("OAuth Controller: Redirecting to return URL: {ReturnUrl}", returnUrl);
            return Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Controller: Error processing OAuth callback for provider {Provider}", provider);
            return BadRequest("OAuth authentication failed");
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var status = new
            {
                GoogleEnabled = _config.EnableGoogleOAuth,
                AppleEnabled = _config.EnableAppleOAuth,
                WizarrIntegrationEnabled = _config.EnableWizarrIntegration,
                RequireValidInvite = _config.RequireValidInvite,
                GoogleProviderAvailable = _googleProvider != null,
                AuthProviderAvailable = _authProvider != null,
                WizarrServiceAvailable = _wizarrService != null
            };

            _logger.LogInformation("OAuth Controller: Status requested - Google: {Google}, Apple: {Apple}, Wizarr: {Wizarr}", 
                status.GoogleEnabled, status.AppleEnabled, status.WizarrIntegrationEnabled);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Controller: Error getting status");
            return BadRequest("Failed to get OAuth status");
        }
    }

    private bool IsProviderEnabled(string provider)
    {
        return provider.ToLower() switch
        {
            "google" => _config.EnableGoogleOAuth,
            "apple" => _config.EnableAppleOAuth,
            _ => false
        };
    }

    private static string GenerateState()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private Task StoreSessionDataAsync(string state, string? inviteCode, string? returnUrl)
    {
        try
        {
            var sessionData = new OAuthSessionData
            {
                InviteCode = inviteCode,
                ReturnUrl = returnUrl,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            var cacheKey = $"oauth_session_{state}";
            var serialized = JsonSerializer.Serialize(sessionData);
            
            _cache.Set(cacheKey, serialized, TimeSpan.FromMinutes(15));
            _logger.LogInformation("OAuth Controller: Stored session data for state: {State}", state);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Controller: Error storing session data for state: {State}", state);
            throw;
        }
    }

    private Task<OAuthSessionData?> GetSessionDataAsync(string state)
    {
        try
        {
            var cacheKey = $"oauth_session_{state}";
            
            if (_cache.TryGetValue(cacheKey, out string? serialized) && !string.IsNullOrEmpty(serialized))
            {
                var sessionData = JsonSerializer.Deserialize<OAuthSessionData>(serialized);
                
                if (sessionData != null && sessionData.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.LogInformation("OAuth Controller: Retrieved valid session data for state: {State}", state);
                    return Task.FromResult<OAuthSessionData?>(sessionData);
                }
                else
                {
                    _logger.LogWarning("OAuth Controller: Session data expired for state: {State}", state);
                }
            }
            else
            {
                _logger.LogWarning("OAuth Controller: No session data found for state: {State}", state);
            }

            return Task.FromResult<OAuthSessionData?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Controller: Error retrieving session data for state: {State}", state);
            return Task.FromResult<OAuthSessionData?>(null);
        }
    }

    private Task CleanupSessionDataAsync(string state)
    {
        try
        {
            var cacheKey = $"oauth_session_{state}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("OAuth Controller: Cleaned up session data for state: {State}", state);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Controller: Error cleaning up session data for state: {State}", state);
            return Task.CompletedTask;
        }
    }
}

