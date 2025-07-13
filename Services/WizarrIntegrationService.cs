using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.OAuth.Authentication.Models;
using Jellyfin.Plugin.OAuth.Configuration;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OAuth.Services;

public class WizarrIntegrationService : IWizarrIntegrationService
{
    private readonly ILogger<WizarrIntegrationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly PluginConfiguration _config;

    public WizarrIntegrationService(
        ILogger<WizarrIntegrationService> logger,
        HttpClient httpClient,
        PluginConfiguration config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config;
        
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_config.WizarrApiUrl);
        
        if (!string.IsNullOrEmpty(_config.WizarrApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _config.WizarrApiKey);
        }
    }

    public async Task<bool> ValidateInviteAsync(string inviteCode)
    {
        try
        {
            if (!_config.EnableWizarrIntegration || string.IsNullOrEmpty(_config.WizarrApiKey))
            {
                _logger.LogWarning("Wizarr integration is disabled or API key is missing");
                return false;
            }

            var response = await _httpClient.GetAsync($"/api/invites/{inviteCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Invalid invite code: {InviteCode}, Status: {StatusCode}", 
                    inviteCode, response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var invite = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (invite.TryGetProperty("used", out var used) && used.GetBoolean())
            {
                _logger.LogWarning("Invite code already used: {InviteCode}", inviteCode);
                return false;
            }

            if (invite.TryGetProperty("expires", out var expires))
            {
                var expirationDate = expires.GetDateTime();
                if (expirationDate < DateTime.UtcNow)
                {
                    _logger.LogWarning("Expired invite code: {InviteCode}", inviteCode);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invite code: {InviteCode}", inviteCode);
            return false;
        }
    }

    public async Task<WizarrInviteSettings> GetInviteSettingsAsync(string inviteCode)
    {
        try
        {
            if (!_config.EnableWizarrIntegration || string.IsNullOrEmpty(_config.WizarrApiKey))
            {
                return new WizarrInviteSettings { IsValid = false };
            }

            var response = await _httpClient.GetAsync($"/api/invites/{inviteCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get invite settings: {InviteCode}, Status: {StatusCode}", 
                    inviteCode, response.StatusCode);
                return new WizarrInviteSettings { IsValid = false };
            }

            var content = await response.Content.ReadAsStringAsync();
            var invite = JsonSerializer.Deserialize<JsonElement>(content);
            
            var settings = new WizarrInviteSettings
            {
                InviteCode = inviteCode,
                IsValid = true
            };

            if (invite.TryGetProperty("libraries", out var libraries))
            {
                var libraryList = new List<string>();
                foreach (var lib in libraries.EnumerateArray())
                {
                    if (lib.TryGetProperty("name", out var name))
                    {
                        libraryList.Add(name.GetString()!);
                    }
                }
                settings.Libraries = libraryList.ToArray();
            }

            if (invite.TryGetProperty("expires", out var expires))
            {
                settings.Expiration = expires.GetDateTime();
            }

            if (invite.TryGetProperty("return_url", out var returnUrl))
            {
                settings.ReturnUrl = returnUrl.GetString();
            }

            settings.UserPolicy = CreateUserPolicyFromInvite(invite);
            
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invite settings: {InviteCode}", inviteCode);
            return new WizarrInviteSettings { IsValid = false };
        }
    }

    public async Task<bool> MarkInviteAsUsedAsync(string inviteCode, Guid userId)
    {
        try
        {
            if (!_config.EnableWizarrIntegration || string.IsNullOrEmpty(_config.WizarrApiKey))
            {
                _logger.LogWarning("Wizarr integration is disabled or API key is missing");
                return false;
            }

            var payload = new
            {
                used = true,
                used_by = userId.ToString(),
                used_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PatchAsync($"/api/invites/{inviteCode}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to mark invite as used: {InviteCode}, Status: {StatusCode}", 
                    inviteCode, response.StatusCode);
                return false;
            }

            _logger.LogInformation("Marked invite as used: {InviteCode} by user {UserId}", 
                inviteCode, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking invite as used: {InviteCode}", inviteCode);
            return false;
        }
    }

    public async Task<bool> IsWizarrAvailableAsync()
    {
        try
        {
            if (!_config.EnableWizarrIntegration)
            {
                return false;
            }

            var response = await _httpClient.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Wizarr availability");
            return false;
        }
    }

    private UserPolicy CreateUserPolicyFromInvite(JsonElement invite)
    {
        var policy = new UserPolicy
        {
            IsAdministrator = false,
            IsHidden = false,
            IsDisabled = false,
            EnableUserPreferenceAccess = true,
            EnableMediaPlayback = true,
            EnableAudioPlaybackTranscoding = true,
            EnableVideoPlaybackTranscoding = true,
            EnablePlaybackRemuxing = true,
            EnableContentDeletion = false,
            EnableContentDownloading = false,
            EnableSyncTranscoding = false,
            EnableLiveTvAccess = false,
            EnableLiveTvManagement = false,
            EnableSharedDeviceControl = false,
            EnableRemoteAccess = true,
            EnablePublicSharing = false,
            EnableMediaConversion = false
        };

        if (invite.TryGetProperty("permissions", out var permissions))
        {
            if (permissions.TryGetProperty("download", out var download))
            {
                policy.EnableContentDownloading = download.GetBoolean();
            }
            
            if (permissions.TryGetProperty("live_tv", out var liveTv))
            {
                policy.EnableLiveTvAccess = liveTv.GetBoolean();
            }
            
            if (permissions.TryGetProperty("admin", out var admin))
            {
                policy.IsAdministrator = admin.GetBoolean();
            }
        }

        return policy;
    }
}