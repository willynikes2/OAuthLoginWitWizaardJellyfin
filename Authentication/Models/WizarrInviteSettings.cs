using System;
using MediaBrowser.Model.Users;

namespace Jellyfin.Plugin.OAuth.Authentication.Models;

public class WizarrInviteSettings
{
    public string[]? Libraries { get; set; }
    public UserPolicy? UserPolicy { get; set; }
    public DateTime? Expiration { get; set; }
    public string? ReturnUrl { get; set; }
    public bool IsValid { get; set; } = true;
    public string? InviteCode { get; set; }
}