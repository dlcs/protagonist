using System.Collections.Generic;
using API.Settings;

namespace API.Tests.Settings;

public class ApiSettingsTests
{
    [Fact]
    public void ApiSettings_LegacySupportDisabledForAllCustomers_byDefault()
    {
        // Arrange
        var settings = new ApiSettings();
        
        // Assert
        settings.GetCustomerSettings(1).LegacySupport.Should().BeFalse();
    }
    
    [Fact]
    public void ApiSettings_LegacySupportEnabledForAllCustomers_WhenDefaultLegacyEnabled()
    {
        // Arrange
        var settings = new ApiSettings
        {
            DefaultLegacySupport = true
        };

        // Assert
        settings.GetCustomerSettings(1).LegacySupport.Should().BeTrue();
    }
    
    [Fact]
    public void ApiSettings_LegacySupportEnabledForSingleCustomer_WhenDefaultLegacyDisabled()
    {
        // Arrange
        var settings = new ApiSettings
        {
            DefaultLegacySupport = false
        };

        settings.CustomerOverrides.Add("2", new CustomerOverrideSettings()
        {
            LegacySupport = true,
            NovelSpaces = new List<string>()
            {
                "1"
            }
        });
        
        // Assert
        settings.GetCustomerSettings(1).LegacySupport.Should().BeFalse();
        settings.GetCustomerSettings(2).LegacySupport.Should().BeTrue();
        settings.GetCustomerSettings(2).NovelSpaces.Should().Contain("1");
    }
    
    [Fact]
    public void LegacyModeEnabledForSpace_LegacySupportDisabledForAllCustomers_byDefault()
    {
        // Arrange
        var settings = new ApiSettings();
        
        // Assert
        settings.LegacyModeEnabledForSpace(1,1).Should().BeFalse();
    }
    
    [Fact]
    public void LegacyModeEnabledForSpace_LegacySupportEnabledForAllCustomers_WhenDefaultLegacySupportEnabled()
    {
        // Arrange
        var settings = new ApiSettings()
        {
            DefaultLegacySupport = true
        };
        
        // Assert
        settings.LegacyModeEnabledForSpace(1,1).Should().BeTrue();
    }
    
    [Fact]
    public void LegacyModeEnabledForSpace_LegacySupportEnabledForSingleCustomer_WhenDefaultLegacySupportDisabled()
    {
        // Arrange
        var settings = new ApiSettings();

        settings.CustomerOverrides.Add("2", new CustomerOverrideSettings()
        {
            LegacySupport = true,
            NovelSpaces = new List<string>()
            {
                "1"
            }
        });
        
        // Assert
        settings.LegacyModeEnabledForSpace(1, 1).Should().BeFalse();
        settings.LegacyModeEnabledForSpace(2, 1).Should().BeFalse();
        settings.LegacyModeEnabledForSpace(2, 2).Should().BeTrue();
    }
    
    [Fact]
    public void LegacyModeEnabledForSpace_LegacySupportDisabledForSpace_WhenDefaultLegacyEnabled()
    {
        // Arrange
        var settings = new ApiSettings()
        {
            DefaultLegacySupport = true
        };
        
        settings.CustomerOverrides.Add("2", new CustomerOverrideSettings()
        {
            LegacySupport = true,
            NovelSpaces = new List<string>()
            {
                "1"
            }
        });
        
        settings.CustomerOverrides.Add("3", new CustomerOverrideSettings()
        {
            LegacySupport = false
        });
        
        // Assert
        settings.LegacyModeEnabledForSpace(1, 1).Should().BeTrue();
        settings.LegacyModeEnabledForSpace(2, 1).Should().BeFalse();
        settings.LegacyModeEnabledForSpace(2, 2).Should().BeTrue();
    }

    [Fact]
    public void LegacyModeEnabledForSpace_LegacySupportDisabledForCustomer_WhenDefaultLegacyEnabled()
    {
        // Arrange
        var settings = new ApiSettings()
        {
            DefaultLegacySupport = true
        };
        
        settings.CustomerOverrides.Add("2", new CustomerOverrideSettings()
        {
            LegacySupport = false,
        });
        
        // Assert
        settings.LegacyModeEnabledForSpace(1, 1).Should().BeTrue();
        settings.LegacyModeEnabledForSpace(2, 1).Should().BeFalse();
    }
    
    [Fact]
    public void LegacyModeEnabledForCustomer_LegacySupportDisabledForAllCustomers_byDefault()
    {
        // Arrange
        var settings = new ApiSettings();
        
        // Assert
        settings.LegacyModeEnabledForCustomer(1).Should().BeFalse();
    }
    
    [Fact]
    public void LegacyModeEnabledForCustomer_LegacySupportDisabledForAllCustomers_WhenDefaultLegacySupportDisabled()
    {
        // Arrange
        var settings = new ApiSettings()
        {
            DefaultLegacySupport = true
        };
        
        // Assert
        settings.LegacyModeEnabledForCustomer(1).Should().BeTrue();
    }
    
    [Fact]
    public void LegacyModeEnabledForCustomer_LegacySupportEnabledForCustomer_WhenDefaultLegacySupportDisabled()
    {
        // Arrange
        var settings = new ApiSettings();
        
        settings.CustomerOverrides.Add("2", new CustomerOverrideSettings()
        {
            LegacySupport = true,
            NovelSpaces = new List<string>()
            {
                "1"
            }
        });
        
        // Assert
        settings.LegacyModeEnabledForCustomer(2).Should().BeTrue();
    }
    
    [Fact]
    public void LegacyModeEnabledForCustomer_LegacySupportDisabledForCustomer_WhenDefaultLegacySupportEnabled()
    {
        // Arrange
        var settings = new ApiSettings()
        {
            DefaultLegacySupport = true
        };
        
        settings.CustomerOverrides.Add("2", new CustomerOverrideSettings()
        {
            LegacySupport = false,
            NovelSpaces = new List<string>()
            {
                "1"
            }
        });
        
        // Assert
        settings.LegacyModeEnabledForCustomer(2).Should().BeFalse();
    }
}