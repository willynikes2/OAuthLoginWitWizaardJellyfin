using System.Threading.Tasks;
using Jellyfin.Plugin.OAuth.Authentication.Models;
using Jellyfin.Data.Entities;

namespace Jellyfin.Plugin.OAuth.Services;

public interface IUserProvisioningService
{
    Task<User> ProvisionUserAsync(OAuthUser oauthUser, string? inviteCode = null);
    Task ApplyWizarrSettingsToUserAsync(User user, WizarrInviteSettings settings);
    Task<User?> FindExistingUserAsync(string email, string providerId);
}