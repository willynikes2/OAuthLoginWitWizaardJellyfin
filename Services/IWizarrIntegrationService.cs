using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.OAuth.Authentication.Models;

namespace Jellyfin.Plugin.OAuth.Services;

public interface IWizarrIntegrationService
{
    Task<bool> ValidateInviteAsync(string inviteCode);
    Task<WizarrInviteSettings> GetInviteSettingsAsync(string inviteCode);
    Task<bool> MarkInviteAsUsedAsync(string inviteCode, Guid userId);
    Task<bool> IsWizarrAvailableAsync();
}