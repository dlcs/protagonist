using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using DLCS.AWS.Configuration;
using DLCS.AWS.Settings;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Tests.Configuration;

public class CustomerScopedAwsClientProviderTests
{
    private readonly ICustomerAwsContext customerAwsContext = new AsyncLocalCustomerAwsContext();
    private readonly ICustomerAwsCredentials customerAwsCredentials = A.Fake<ICustomerAwsCredentials>();
    private readonly CustomerScopedAwsClientProvider<IAmazonS3> sut;

    public CustomerScopedAwsClientProviderTests()
    {
        A.CallTo(() => customerAwsCredentials.GetCredentials(A<int>._)).Returns(new AnonymousAWSCredentials());

        var awsSettings = Options.Create(new AWSSettings
        {
            AssumeRole = new AssumeRoleSettings { Enabled = true, RoleArn = "arn:aws:iam::123456789012:role/Engine" }
        });

        sut = new CustomerScopedAwsClientProvider<IAmazonS3>(customerAwsContext, customerAwsCredentials, awsSettings,
            NullLogger<CustomerScopedAwsClientProvider<IAmazonS3>>.Instance,
            new AWSOptions { Region = RegionEndpoint.EUWest1 });
    }

    [Fact]
    public void GetClient_Throws_IfNoCustomerSet()
    {
        // Falling back to ambient credentials here would give access to every customers assets
        Action action = () => sut.GetClient();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetClient_ReturnsClient_IfCustomerSet()
    {
        using var customerScope = customerAwsContext.SetCustomer(99);

        sut.GetClient().Should().NotBeNull();
    }

    [Fact]
    public void GetClient_ReturnsSameClient_ForSameCustomer()
    {
        // Arrange
        using var customerScope = customerAwsContext.SetCustomer(99);

        // Act
        var first = sut.GetClient();
        var second = sut.GetClient();

        // Assert
        second.Should().BeSameAs(first);
        A.CallTo(() => customerAwsCredentials.GetCredentials(99)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GetClient_CreatesSingleClient_IfCalledConcurrentlyForSameCustomer()
    {
        // A duplicate would mean an additional STS session, which are rate limited per account
        // Arrange
        A.CallTo(() => customerAwsCredentials.GetCredentials(A<int>._)).ReturnsLazily(() =>
        {
            Thread.Sleep(20);
            return new AnonymousAWSCredentials();
        });

        using var customerScope = customerAwsContext.SetCustomer(99);
        var clients = new IAmazonS3[16];

        // Act
        Parallel.For(0, clients.Length, i => clients[i] = sut.GetClient());

        // Assert
        clients.Should().AllBeEquivalentTo(clients[0]).And.OnlyContain(c => ReferenceEquals(c, clients[0]));
        A.CallTo(() => customerAwsCredentials.GetCredentials(99)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GetClient_ReturnsDifferentClient_PerCustomer()
    {
        // Arrange
        IAmazonS3 firstCustomerClient;
        IAmazonS3 secondCustomerClient;

        // Act
        using (customerAwsContext.SetCustomer(10))
        {
            firstCustomerClient = sut.GetClient();
        }

        using (customerAwsContext.SetCustomer(20))
        {
            secondCustomerClient = sut.GetClient();
        }

        // Assert
        secondCustomerClient.Should().NotBeSameAs(firstCustomerClient);
        A.CallTo(() => customerAwsCredentials.GetCredentials(10)).MustHaveHappenedOnceExactly();
        A.CallTo(() => customerAwsCredentials.GetCredentials(20)).MustHaveHappenedOnceExactly();
    }
}
