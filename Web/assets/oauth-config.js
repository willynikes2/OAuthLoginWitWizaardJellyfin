define(['loading', 'dialogHelper', 'formDialogStyle'], function(loading, dialogHelper) {
    'use strict';

    var pluginId = 'e4a2f2b6-8c3a-4b9d-9e1f-2a3b4c5d6e7f';
    
    function showMessage(message, isError) {
        var messageContainer = document.getElementById('messageContainer');
        var messageContent = document.getElementById('messageContent');
        
        messageContent.textContent = message;
        messageContent.className = isError ? 'alert alert-error' : 'alert alert-info';
        messageContainer.style.display = 'block';
        
        setTimeout(function() {
            messageContainer.style.display = 'none';
        }, 5000);
    }
    
    function loadConfiguration() {
        loading.show();
        
        ApiClient.getPluginConfiguration(pluginId).then(function(config) {
            console.log('Loaded configuration:', config);
            
            // General Settings
            document.getElementById('autoCreateUsers').checked = config.AutoCreateUsers || false;
            
            // Google OAuth Settings
            document.getElementById('enableGoogleOAuth').checked = config.EnableGoogleOAuth || false;
            document.getElementById('googleClientId').value = (config.GoogleSettings && config.GoogleSettings.ClientId) || '';
            document.getElementById('googleClientSecret').value = (config.GoogleSettings && config.GoogleSettings.ClientSecret) || '';
            document.getElementById('googleScopes').value = (config.GoogleSettings && config.GoogleSettings.Scopes) || 'email profile';
            document.getElementById('googleRedirectUri').value = (config.GoogleSettings && config.GoogleSettings.RedirectUri) || 'http://localhost:8096/oauth/google/callback';
            
            // Apple OAuth Settings
            document.getElementById('enableAppleOAuth').checked = config.EnableAppleOAuth || false;
            document.getElementById('appleServicesId').value = (config.AppleSettings && config.AppleSettings.ServicesId) || '';
            document.getElementById('appleTeamId').value = (config.AppleSettings && config.AppleSettings.TeamId) || '';
            document.getElementById('appleKeyId').value = (config.AppleSettings && config.AppleSettings.KeyId) || '';
            document.getElementById('applePrivateKey').value = (config.AppleSettings && config.AppleSettings.PrivateKey) || '';
            document.getElementById('appleRedirectUri').value = (config.AppleSettings && config.AppleSettings.RedirectUri) || 'https://localhost:8096/oauth/apple/callback';
            
            // Wizarr Integration Settings
            document.getElementById('enableWizarrIntegration').checked = config.EnableWizarrIntegration || false;
            document.getElementById('wizarrApiUrl').value = config.WizarrApiUrl || 'http://localhost:5690';
            document.getElementById('wizarrApiKey').value = config.WizarrApiKey || '';
            document.getElementById('requireValidInvite').checked = config.RequireValidInvite || false;
            
            toggleWizarrSettings();
            toggleGoogleSettings();
            toggleAppleSettings();
            updateTestUrls();
            
            loading.hide();
        }).catch(function(error) {
            console.error('Error loading configuration:', error);
            loading.hide();
            showMessage('Error loading configuration: ' + (error.message || error), true);
        });
    }
    
    function saveConfiguration() {
        loading.show();
        
        var config = {
            // General Settings
            AutoCreateUsers: document.getElementById('autoCreateUsers').checked,
            
            // Google OAuth Settings
            EnableGoogleOAuth: document.getElementById('enableGoogleOAuth').checked,
            GoogleSettings: {
                ClientId: document.getElementById('googleClientId').value,
                ClientSecret: document.getElementById('googleClientSecret').value,
                Scopes: document.getElementById('googleScopes').value,
                RedirectUri: document.getElementById('googleRedirectUri').value
            },
            
            // Apple OAuth Settings
            EnableAppleOAuth: document.getElementById('enableAppleOAuth').checked,
            AppleSettings: {
                ServicesId: document.getElementById('appleServicesId').value,
                TeamId: document.getElementById('appleTeamId').value,
                KeyId: document.getElementById('appleKeyId').value,
                PrivateKey: document.getElementById('applePrivateKey').value,
                RedirectUri: document.getElementById('appleRedirectUri').value
            },
            
            // Wizarr Integration Settings
            EnableWizarrIntegration: document.getElementById('enableWizarrIntegration').checked,
            WizarrApiUrl: document.getElementById('wizarrApiUrl').value,
            WizarrApiKey: document.getElementById('wizarrApiKey').value,
            RequireValidInvite: document.getElementById('requireValidInvite').checked
        };
        
        console.log('Saving configuration:', config);
        
        ApiClient.updatePluginConfiguration(pluginId, config).then(function() {
            loading.hide();
            showMessage('Configuration saved successfully!', false);
            console.log('Configuration saved successfully');
        }).catch(function(error) {
            console.error('Error saving configuration:', error);
            loading.hide();
            showMessage('Error saving configuration: ' + (error.message || error), true);
        });
    }
    
    function resetToDefaults() {
        if (confirm('Are you sure you want to reset all settings to their default values?')) {
            // General Settings
            document.getElementById('autoCreateUsers').checked = true;
            
            // Google OAuth Settings
            document.getElementById('enableGoogleOAuth').checked = false;
            document.getElementById('googleClientId').value = '';
            document.getElementById('googleClientSecret').value = '';
            document.getElementById('googleScopes').value = 'email profile';
            document.getElementById('googleRedirectUri').value = 'http://localhost:8096/oauth/google/callback';
            
            // Apple OAuth Settings
            document.getElementById('enableAppleOAuth').checked = false;
            document.getElementById('appleServicesId').value = '';
            document.getElementById('appleTeamId').value = '';
            document.getElementById('appleKeyId').value = '';
            document.getElementById('applePrivateKey').value = '';
            document.getElementById('appleRedirectUri').value = 'https://localhost:8096/oauth/apple/callback';
            
            // Wizarr Integration Settings
            document.getElementById('enableWizarrIntegration').checked = false;
            document.getElementById('wizarrApiUrl').value = 'http://localhost:5690';
            document.getElementById('wizarrApiKey').value = '';
            document.getElementById('requireValidInvite').checked = false;
            
            toggleWizarrSettings();
            toggleGoogleSettings();
            toggleAppleSettings();
            updateTestUrls();
            
            showMessage('Settings reset to defaults. Click "Save Configuration" to persist changes.', false);
        }
    }
    
    function toggleWizarrSettings() {
        var enabled = document.getElementById('enableWizarrIntegration').checked;
        document.getElementById('wizarrApiUrl').disabled = !enabled;
        document.getElementById('wizarrApiKey').disabled = !enabled;
        document.getElementById('requireValidInvite').disabled = !enabled;
    }
    
    function toggleGoogleSettings() {
        var enabled = document.getElementById('enableGoogleOAuth').checked;
        document.getElementById('googleClientId').disabled = !enabled;
        document.getElementById('googleClientSecret').disabled = !enabled;
        document.getElementById('googleScopes').disabled = !enabled;
        document.getElementById('googleRedirectUri').disabled = !enabled;
    }
    
    function toggleAppleSettings() {
        var enabled = document.getElementById('enableAppleOAuth').checked;
        document.getElementById('appleServicesId').disabled = !enabled;
        document.getElementById('appleTeamId').disabled = !enabled;
        document.getElementById('appleKeyId').disabled = !enabled;
        document.getElementById('applePrivateKey').disabled = !enabled;
        document.getElementById('appleRedirectUri').disabled = !enabled;
    }
    
    function updateTestUrls() {
        // Update Google test URL
        var googleRedirectUri = document.getElementById('googleRedirectUri').value;
        var googleTestUrlElement = document.getElementById('googleTestUrl');
        if (googleRedirectUri && googleRedirectUri.includes('/oauth/google/callback')) {
            var googleTestUrl = googleRedirectUri.replace('/oauth/google/callback', '/oauth/google/authorize');
            googleTestUrlElement.textContent = googleTestUrl;
        } else {
            googleTestUrlElement.textContent = 'Configure redirect URI to see test URL';
        }
        
        // Update Apple test URL
        var appleRedirectUri = document.getElementById('appleRedirectUri').value;
        var appleTestUrlElement = document.getElementById('appleTestUrl');
        if (appleRedirectUri && appleRedirectUri.includes('/oauth/apple/callback')) {
            var appleTestUrl = appleRedirectUri.replace('/oauth/apple/callback', '/oauth/apple/authorize');
            appleTestUrlElement.textContent = appleTestUrl;
        } else {
            appleTestUrlElement.textContent = 'Configure redirect URI to see test URL';
        }
    }
    
    function attachEventHandlers() {
        // Toggle settings handlers
        document.getElementById('enableWizarrIntegration').addEventListener('change', toggleWizarrSettings);
        document.getElementById('enableGoogleOAuth').addEventListener('change', toggleGoogleSettings);
        document.getElementById('enableAppleOAuth').addEventListener('change', toggleAppleSettings);
        
        // Update test URLs when redirect URIs change
        document.getElementById('googleRedirectUri').addEventListener('input', updateTestUrls);
        document.getElementById('appleRedirectUri').addEventListener('input', updateTestUrls);
        
        // Save button handler
        document.getElementById('saveButton').addEventListener('click', saveConfiguration);
        
        // Reset button handler
        document.getElementById('resetButton').addEventListener('click', resetToDefaults);
    }
    
    // Return the view object
    return {
        init: function() {
            attachEventHandlers();
            loadConfiguration();
        }
    };
});