using System.Collections.Generic;
using System.Security.Claims;
using API.Client;
using DLCS.Web.Auth;
using FluentAssertions;
using Xunit;

namespace Portal.Tests
{
    public class ClaimsPrincipalXTests
    {
        [Fact]
        public void GetCustomerId_Null_IfClaimNotFound()
        {
            // Arrange
            var identity = new ClaimsIdentity(new List<Claim> {new("Name", "Test")}, "Whatever");
            var principal = new ClaimsPrincipal(new[] {identity});
            
            // Act
            var customerId = principal.GetCustomerId();

            // Assert
            customerId.Should().BeNull();
        }
        
        [Fact]
        public void GetCustomerId_Null_IfClaimNotNumeric()
        {
            // Arrange
            var identity = new ClaimsIdentity(new List<Claim> {new("Customer", "TestCustomer")}, "Whatever");
            var principal = new ClaimsPrincipal(new[] {identity});

            // Act
            var customerId = principal.GetCustomerId();

            // Assert
            customerId.Should().BeNull();
        }
        
        [Fact]
        public void GetCustomerId_ReturnsCorrectValue()
        {
            // Arrange
            var identity = new ClaimsIdentity(new List<Claim> {new("Customer", "123")}, "Whatever");
            var principal = new ClaimsPrincipal(new[] {identity});

            // Act
            var customerId = principal.GetCustomerId();

            // Assert
            customerId.Should().Be(123);
        }
        
        [Fact]
        public void GetApiCredentials_Null_IfClaimNotFound()
        {
            // Arrange
            var identity = new ClaimsIdentity(new List<Claim> {new("Name", "Test")}, "Whatever");
            var principal = new ClaimsPrincipal(new[] {identity});
            
            // Act
            var customerId = principal.GetApiCredentials();

            // Assert
            customerId.Should().BeNull();
        }

        [Fact]
        public void GetApiCredentials_ReturnsCorrectValue()
        {
            // Arrange
            var identity = new ClaimsIdentity(new List<Claim> {new("ApiCredentials", "ssshhhh")}, "Whatever");
            var principal = new ClaimsPrincipal(new[] {identity});

            // Act
            var customerId = principal.GetApiCredentials();

            // Assert
            customerId.Should().Be("ssshhhh");
        }
    }
}