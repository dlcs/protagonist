using System;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;

namespace Orchestrator.Tests.Infrastructure;

public class GatewayTokenGeneratorTests
{
    private const string Secret = "shared-secret";
    private const string Identifier = "s3:%2F%2Fbucket%2Fkey";

    // 2026-01-01T00:00:00Z, unix 1767225600. With a 1800s window this is bucket 981792
    private static readonly DateTimeOffset FixedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetToken_ReturnsNull_IfNoSecretConfigured()
    {
        var sut = GetSut(new GatewayTokenSettings());

        sut.IsEnabled.Should().BeFalse();
        sut.GetToken(Identifier).Should().BeNull();
    }

    [Fact]
    public void Ctor_Throws_IfSecretSetButWindowNotPositive()
    {
        Action action = () => GetSut(new GatewayTokenSettings { Secret = Secret, WindowSecs = 0 });

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetToken_ReturnsKnownSignature()
    {
        // This pins the signed value format, "orch|v1|{bucket}|{identifier}", that the image-server recomputes.
        var sut = GetSut(new GatewayTokenSettings { Secret = Secret, WindowSecs = 1800 }, FixedTime);

        var token = sut.GetToken(Identifier);

        token.Should().Be("3679d34ec1cca2b962c045cefa2730ddec8bf41073c8f354666c26bf5e0e0ce3");
    }

    [Fact]
    public void GetToken_SameValue_ForDifferentTimesInSameWindow()
    {
        var settings = new GatewayTokenSettings { Secret = Secret, WindowSecs = 1800 };
        var startOfWindow = GetSut(settings, FixedTime).GetToken(Identifier);
        var endOfWindow = GetSut(settings, FixedTime.AddSeconds(1799)).GetToken(Identifier);

        endOfWindow.Should().Be(startOfWindow);
    }

    [Fact]
    public void GetToken_DifferentValue_ForNextWindow()
    {
        var settings = new GatewayTokenSettings { Secret = Secret, WindowSecs = 1800 };
        var currentWindow = GetSut(settings, FixedTime).GetToken(Identifier);
        var nextWindow = GetSut(settings, FixedTime.AddSeconds(1800)).GetToken(Identifier);

        nextWindow.Should().NotBe(currentWindow);
    }

    [Fact]
    public void GetToken_DifferentValue_ForDifferentIdentifier()
    {
        var sut = GetSut(new GatewayTokenSettings { Secret = Secret, WindowSecs = 1800 }, FixedTime);

        sut.GetToken(Identifier).Should().NotBe(sut.GetToken("s3:%2F%2Fbucket%2Fother-key"));
    }

    [Fact]
    public void GetToken_DifferentValue_ForDifferentSecret()
    {
        var token = GetSut(new GatewayTokenSettings { Secret = Secret, WindowSecs = 1800 }, FixedTime)
            .GetToken(Identifier);
        var rotatedToken = GetSut(new GatewayTokenSettings { Secret = "next-secret", WindowSecs = 1800 }, FixedTime)
            .GetToken(Identifier);

        rotatedToken.Should().NotBe(token);
    }

    private static GatewayTokenGenerator GetSut(GatewayTokenSettings gatewayToken, DateTimeOffset? utcNow = null)
    {
        var timeProvider = A.Fake<TimeProvider>();
        A.CallTo(() => timeProvider.GetUtcNow()).Returns(utcNow ?? FixedTime);

        return new GatewayTokenGenerator(Options.Create(new OrchestratorSettings { GatewayToken = gatewayToken }),
            new NullLogger<GatewayTokenGenerator>(),
            timeProvider);
    }
}
