using System.Collections.Generic;
using API.Settings;

namespace API.Tests.Settings;

public class ApiSettingsTests
{
    [Fact]
    public void ApiSettings_LegacySupportEnabledForAllCustomers_byDefault()
    {
        // Arrange
        var settings = new ApiSettings();
        
        // Assert
        settings.GetCustomerSettings(1).LegacySupport.Should().BeTrue();
    }
    
    [Fact]
    public void ApiSettings_LegacySupportDisabledForAllCustomers_WhenDefaultLegacyDisabled()
    {
        // Arrange
        var settings = new ApiSettings
        {
            DefaultLegacySupport = false
        };

        // Assert
        settings.GetCustomerSettings(1).LegacySupport.Should().BeFalse();
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
}