using MediaBrowser.Model.Plugins;
using System.Xml.Serialization;

namespace Jellyfin.Plugin.OAuth.Configuration;

[XmlRoot("PluginConfiguration")]
public class PluginConfiguration : BasePluginConfiguration
{
    [XmlElement("EnableGoogleOAuth")]
    public bool EnableGoogleOAuth { get; set; } = false;
    
    [XmlElement("EnableAppleOAuth")]
    public bool EnableAppleOAuth { get; set; } = false;
    
    [XmlElement("AutoCreateUsers")]
    public bool AutoCreateUsers { get; set; } = true;
    
    [XmlArray("DefaultUserRoles")]
    [XmlArrayItem("Role")]
    public string[] DefaultUserRoles { get; set; } = { "User" };
    
    [XmlElement("EnableWizarrIntegration")]
    public bool EnableWizarrIntegration { get; set; } = false;
    
    [XmlElement("WizarrApiUrl")]
    public string WizarrApiUrl { get; set; } = "http://localhost:5690";
    
    [XmlElement("WizarrApiKey")]
    public string WizarrApiKey { get; set; } = string.Empty;
    
    [XmlElement("RequireValidInvite")]
    public bool RequireValidInvite { get; set; } = false;
    
    [XmlElement("GoogleSettings")]
    public GoogleOAuthSettings GoogleSettings { get; set; } = new();
    
    [XmlElement("AppleSettings")]
    public AppleOAuthSettings AppleSettings { get; set; } = new();
}

[XmlRoot("GoogleOAuthSettings")]
public class GoogleOAuthSettings
{
    [XmlElement("ClientId")]
    public string ClientId { get; set; } = string.Empty;
    
    [XmlElement("ClientSecret")]
    public string ClientSecret { get; set; } = string.Empty;
    
    [XmlElement("Scopes")]
    public string Scopes { get; set; } = "email profile";
    
    [XmlElement("RedirectUri")]
    public string RedirectUri { get; set; } = "http://localhost:8096/oauth/google/callback";
}

[XmlRoot("AppleOAuthSettings")]
public class AppleOAuthSettings
{
    [XmlElement("ServicesId")]
    public string ServicesId { get; set; } = string.Empty;
    
    [XmlElement("TeamId")]
    public string TeamId { get; set; } = string.Empty;
    
    [XmlElement("KeyId")]
    public string KeyId { get; set; } = string.Empty;
    
    [XmlElement("PrivateKey")]
    public string PrivateKey { get; set; } = string.Empty;
    
    [XmlElement("Scopes")]
    public string Scopes { get; set; } = "name email";
    
    [XmlElement("RedirectUri")]
    public string RedirectUri { get; set; } = "https://localhost:8096/oauth/apple/callback";
}