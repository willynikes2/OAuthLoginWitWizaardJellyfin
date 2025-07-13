using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using Jellyfin.Plugin.OAuth.Authentication;
using Jellyfin.Plugin.OAuth.Authentication.Providers;
using Jellyfin.Plugin.OAuth.Configuration;
using Jellyfin.Plugin.OAuth.Services;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OAuth;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IPluginServiceRegistrator
{
    public override string Name => "OAuth Authentication";

    public override Guid Id => Guid.Parse("e4a2f2b6-8c3a-4b9d-9e1f-2a3b4c5d6e7f");

    public override string Description => "Provides OAuth 2.0 authentication with Google and Apple Sign-In, featuring seamless Wizarr integration for user provisioning and invite management.";

    private readonly ILogger<Plugin> _logger;

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger) : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
        
        _logger.LogInformation("OAuth Plugin: Initializing plugin");
        
        // Initialize configuration if it doesn't exist
        if (Configuration == null)
        {
            _logger.LogInformation("OAuth Plugin: Configuration is null, creating new configuration");
            UpdateConfiguration(new PluginConfiguration());
        }
        else
        {
            _logger.LogInformation("OAuth Plugin: Configuration loaded successfully");
        }
    }

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = this.Name,
                EmbeddedResourcePath = GetType().Namespace + ".Web.configPage.html"
            }
        };
    }

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        _logger.LogInformation("OAuth Plugin: UpdateConfiguration called");
        
        if (configuration is PluginConfiguration oauthConfig)
        {
            _logger.LogInformation("OAuth Plugin: Updating configuration with Google OAuth enabled: {GoogleEnabled}, Apple OAuth enabled: {AppleEnabled}", 
                oauthConfig.EnableGoogleOAuth, oauthConfig.EnableAppleOAuth);
        }
        
        base.UpdateConfiguration(configuration);
        
        // Explicitly save the configuration
        try
        {
            SaveConfiguration();
            _logger.LogInformation("OAuth Plugin: Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Plugin: Failed to save configuration");
            throw;
        }
    }

    public override void SaveConfiguration()
    {
        _logger.LogInformation("OAuth Plugin: SaveConfiguration called");
        
        try
        {
            base.SaveConfiguration();
            _logger.LogInformation("OAuth Plugin: Base SaveConfiguration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth Plugin: Error in SaveConfiguration");
            throw;
        }
    }

    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        Console.WriteLine("OAuth: RegisterServices called");
        _logger.LogInformation("OAuth Plugin: RegisterServices called");
        
        try
        {
            Console.WriteLine("OAuth: Starting service registration...");
            
            // 1. Register plugin configuration as singleton first - all other services depend on this
            Console.WriteLine("OAuth: Registering PluginConfiguration");
            serviceCollection.AddSingleton<PluginConfiguration>(provider => 
            {
                var config = this.Configuration;
                if (config == null)
                {
                    Console.WriteLine("OAuth: Configuration is null in service factory, creating default");
                    _logger.LogWarning("OAuth Plugin: Configuration is null in service factory, creating default");
                    config = new PluginConfiguration();
                }
                Console.WriteLine($"OAuth: Configuration factory created config - GoogleEnabled: {config.EnableGoogleOAuth}");
                return config;
            });

            // 2. Register core dependencies
            Console.WriteLine("OAuth: Registering HttpClient");
            serviceCollection.AddSingleton<HttpClient>();
            
            Console.WriteLine("OAuth: Registering IMemoryCache");
            serviceCollection.AddSingleton<IMemoryCache, MemoryCache>();

            // 3. Register OAuth providers with explicit factory methods and validation
            Console.WriteLine("OAuth: Registering GoogleOAuthProvider");
            serviceCollection.AddTransient<GoogleOAuthProvider>(provider =>
            {
                try
                {
                    var logger = provider.GetRequiredService<ILogger<GoogleOAuthProvider>>();
                    var httpClient = provider.GetRequiredService<HttpClient>();
                    var config = provider.GetRequiredService<PluginConfiguration>();
                    
                    Console.WriteLine($"OAuth: Creating GoogleOAuthProvider with config - GoogleEnabled: {config.EnableGoogleOAuth}");
                    logger.LogInformation("OAuth Plugin: Creating GoogleOAuthProvider instance");
                    
                    return new GoogleOAuthProvider(logger, httpClient, config);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OAuth: Error creating GoogleOAuthProvider: {ex.Message}");
                    throw;
                }
            });
            
            // 4. Register authentication provider
            Console.WriteLine("OAuth: Registering OAuthAuthenticationProvider");
            serviceCollection.AddTransient<OAuthAuthenticationProvider>();
            
            // 5. Register application services
            Console.WriteLine("OAuth: Registering IUserProvisioningService");
            serviceCollection.AddTransient<IUserProvisioningService, UserProvisioningService>();
            
            Console.WriteLine("OAuth: Registering IWizarrIntegrationService");
            serviceCollection.AddTransient<IWizarrIntegrationService, WizarrIntegrationService>();
            
            // 6. Register MVC and controller for Jellyfin routing system
            try
            {
                Console.WriteLine("OAuth: Registering MVC controllers");
                serviceCollection.AddMvc().AddApplicationPart(typeof(Controllers.OAuthController).Assembly);
                _logger.LogInformation("OAuth Plugin: MVC controllers registered successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OAuth: Error registering MVC controllers: {ex.Message}");
                _logger.LogError(ex, "OAuth Plugin: Error registering MVC controllers");
                // Don't throw here - continue with other registrations
            }
            
            Console.WriteLine("OAuth: All services registered successfully");
            _logger.LogInformation("OAuth Plugin: All services registered successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OAuth: FATAL - Error registering services: {ex.Message}");
            Console.WriteLine($"OAuth: Stack trace: {ex.StackTrace}");
            _logger.LogError(ex, "OAuth Plugin: Error registering services");
            throw;
        }
    }
}

