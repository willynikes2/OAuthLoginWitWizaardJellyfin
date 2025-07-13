using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.OAuth.Authentication.Models;
using Jellyfin.Plugin.OAuth.Configuration;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OAuth.Services;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly ILogger<UserProvisioningService> _logger;
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IWizarrIntegrationService _wizarrService;
    private readonly PluginConfiguration _config;

    public UserProvisioningService(
        ILogger<UserProvisioningService> logger,
        IUserManager userManager,
        ILibraryManager libraryManager,
        IWizarrIntegrationService wizarrService,
        PluginConfiguration config)
    {
        _logger = logger;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _wizarrService = wizarrService;
        _config = config;
    }

    public async Task<User> ProvisionUserAsync(OAuthUser oauthUser, string? inviteCode = null)
    {
        try
        {
            WizarrInviteSettings? wizarrSettings = null;
            
            if (!string.IsNullOrEmpty(inviteCode) && _config.EnableWizarrIntegration)
            {
                wizarrSettings = await _wizarrService.GetInviteSettingsAsync(inviteCode);
            }

            var existingUser = await FindExistingUserAsync(oauthUser.Email, oauthUser.Id);
            
            if (existingUser != null)
            {
                _logger.LogInformation("Found existing user {Email} for OAuth provider {Provider}", 
                    oauthUser.Email, oauthUser.Provider);
                
                if (wizarrSettings != null)
                {
                    await ApplyWizarrSettingsToUserAsync(existingUser, wizarrSettings);
                }
                
                return existingUser;
            }

            if (!_config.AutoCreateUsers)
            {
                throw new InvalidOperationException("User does not exist and auto-creation is disabled");
            }

            var newUser = await CreateUserAsync(oauthUser, wizarrSettings);
            
            _logger.LogInformation("Created new user {Email} from OAuth provider {Provider}", 
                oauthUser.Email, oauthUser.Provider);
            
            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision user {Email} from provider {Provider}", 
                oauthUser.Email, oauthUser.Provider);
            throw;
        }
    }

    public Task<User?> FindExistingUserAsync(string email, string providerId)
    {
        var users = _userManager.Users;
        
        // For now, let's just find by username since Jellyfin.Data.Entities.User has different properties
        var userByName = users.FirstOrDefault(u => 
            string.Equals(u.Username, email, StringComparison.OrdinalIgnoreCase));
        
        return Task.FromResult(userByName);
    }

    private async Task<User> CreateUserAsync(OAuthUser oauthUser, WizarrInviteSettings? wizarrSettings)
    {
        var username = await GenerateUniqueUsernameAsync(oauthUser.Name ?? oauthUser.Email);
        
        // Use the correct Jellyfin.Data.Entities.User constructor
        var user = new User(username, "OAuth", "OAuth");

        var createdUser = await _userManager.CreateUserAsync(user.Username);
        
        if (wizarrSettings != null)
        {
            await ApplyWizarrSettingsToUserAsync(createdUser, wizarrSettings);
        }
        else
        {
            await SetDefaultLibraryAccessAsync(createdUser);
        }

        return createdUser;
    }

    public async Task ApplyWizarrSettingsToUserAsync(User user, WizarrInviteSettings settings)
    {
        // TODO: Apply Wizarr settings to user using the correct Jellyfin API
        // The Jellyfin.Data.Entities.User class doesn't have Policy property
        // We need to use IUserManager methods to set policies
        
        if (settings.Libraries != null && settings.Libraries.Length > 0)
        {
            await SetUserLibraryAccessAsync(user, settings.Libraries);
        }

        await _userManager.UpdateUserAsync(user);
        
        _logger.LogInformation("Applied Wizarr settings to user {Username}", user.Username);
    }

    private Task SetUserLibraryAccessAsync(User user, string[] libraryNames)
    {
        // TODO: Implement library access control using IUserManager
        // For now, just log the attempt
        _logger.LogInformation("Setting library access for user {Username} to libraries: {Libraries}", 
            user.Username, string.Join(", ", libraryNames));
        return Task.CompletedTask;
    }

    private Task SetDefaultLibraryAccessAsync(User user)
    {
        // TODO: Implement default library access using IUserManager
        // For now, just log the attempt
        _logger.LogInformation("Setting default library access for user {Username}", user.Username);
        return Task.CompletedTask;
    }

    // Remove these methods as they don't apply to the current Jellyfin.Data.Entities.User API

    private Task<string> GenerateUniqueUsernameAsync(string baseName)
    {
        var sanitizedName = SanitizeUsername(baseName);
        var username = sanitizedName;
        var counter = 1;

        while (_userManager.GetUserByName(username) != null)
        {
            username = $"{sanitizedName}{counter}";
            counter++;
        }

        return Task.FromResult(username);
    }

    private static string SanitizeUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "OAuthUser";
        }

        var sanitized = input.Replace("@", "").Replace(".", "").Replace(" ", "");
        return string.IsNullOrEmpty(sanitized) ? "OAuthUser" : sanitized;
    }
}