﻿using System;
using System.Collections.Generic;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Parsing;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.Repository.Tests.NamedQueries.Parsing;

public class IIIFNamedQueryParserTests
{
    private readonly IIIFNamedQueryParser sut;
    private const int Customer = 99;

    public IIIFNamedQueryParserTests()
    {
        sut = new IIIFNamedQueryParser(new NullLogger<IIIFNamedQueryParser>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GenerateParsedNamedQueryFromRequest_Throws_IfTemplateEmptyOrWhiteSpace(string template)
    {
        // Act
        Action action = () =>
            sut.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, null, template, "my-query");

        // Assert
        action.Should()
            .ThrowExactly<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'namedQueryTemplate')");
    }

    [Fact]
    public void GenerateParsedNamedQueryFromRequest_ReturnFaultyNQ_IfNoParametersPassed()
    {
        // Act
        var result =
            sut.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, "", "s1=p1&space=p2", "my-query");

        // Assert
        result.IsFaulty.Should().BeTrue();
        result.ErrorMessage.Should().StartWith("Named query must have at least 1 argument");
    }
    
    [Theory]
    [InlineData("s1=p1&space=p2", "1")]
    [InlineData("s1=p1&n1=p2", "1")]
    [InlineData("s1=p1&n1=&n2=p2", "1/2")]
    [InlineData("space=p1&s1=p2&#=1", "")]
    [InlineData("space=p1&s1=p2&#=1", "10")]
    public void GenerateParsedNamedQueryFromRequest_ReturnsNQ_IfLessQueriesPassedThanParameters(string template,
        string args)
    {
        // Act
        var result =
            sut.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, args, template, "my-query");

        // Assert
        result.IsFaulty.Should().BeFalse();
    }

    [Theory]
    [InlineData("space=p1x", "1")]
    [InlineData("space=param", "1")]
    [InlineData("space=p1&s1=pa2&#=1", "")]
    public void GenerateParsedNamedQueryFromRequest_ReturnsFaultParsedNQ_IfInvalidParameterArgPassed(
        string template,
        string args)
    {
        // Act
        var result =
            sut.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, args, template, "my-query");

        // Assert
        result.IsFaulty.Should().BeTrue();
        result.ErrorMessage.Should().StartWith("Could not parse template element parameter");
    }

    [Theory]
    [InlineData("n1=p1", "not-an-int")]
    [InlineData("n1=p1&#=not-an-int", "")]
    [InlineData("batch=p1", "not-an-int")]
    [InlineData("batch=p1", "1|2|3")]
    [InlineData("batch=p1&#=not-an-int", "")]
    public void GenerateParsedNamedQueryFromRequest_ReturnsFaultParsedNQ_IfNonNumberPassedForNumberArg(
        string template,
        string args)
    {
        // Act
        var result =
            sut.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, args, template, "my-query");

        // Assert
        result.IsFaulty.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ParseNamedQueries))]
    public void GenerateParsedNamedQueryFromRequest_SuccessfullyParses(string template, string args,
        IIIFParsedNamedQuery expected, string explanation)
    {
        // Act
        var result =
            sut.GenerateParsedNamedQueryFromRequest<IIIFParsedNamedQuery>(Customer, args, template, "my-query");

        // Assert
        result.Should().BeEquivalentTo(expected, explanation);
    }

    // Note: This is not a completely exhaustive list
    public static IEnumerable<object[]> ParseNamedQueries => new List<object[]>
    {
        new object[]
        {
            "space=p1", "10", new IIIFParsedNamedQuery(Customer) { Space = 10, NamedQueryName = "my-query" },
            "Space from param"
        },
        new object[]
        {
            "space=5", "", new IIIFParsedNamedQuery(Customer) { Space = 5, NamedQueryName = "my-query" },
            "Hardcoded value"
        },
        new object[]
        {
            "space=p1&#=10", "", new IIIFParsedNamedQuery(Customer) { Space = 10, NamedQueryName = "my-query" },
            "Space from template"
        },
        new object[]
        {
            "batch=p1&#=10", "",
            new IIIFParsedNamedQuery(Customer) { Batches = [10], NamedQueryName = "my-query" },
            "Single batch from template"
        },
        new object[]
        {
            "manifest=p1&#=foo-bar", "",
            new IIIFParsedNamedQuery(Customer) { Manifests = ["foo-bar"], NamedQueryName = "my-query" },
            "Single manifest from template"
        },
        new object[]
        {
            "spacename=p1", "10",
            new IIIFParsedNamedQuery(Customer)
                { SpaceName = "10", NamedQueryName = "my-query" },
            "Spacename from param"
        },
        new object[]
        {
            "canvas=n2&s1=p1&n1=p2&space=p3&#=1", "string-1/40",
            new IIIFParsedNamedQuery(Customer)
            {
                String1 = "string-1", Number1 = 40, Space = 1,
                AssetOrdering = new List<ParsedNamedQuery.QueryOrder> { new(ParsedNamedQuery.QueryMapping.Number2) },
                NamedQueryName = "my-query"
            },
            "All params"
        },
        new object[]
        {
            "sequence=n1&canvas=n2&s1=p1&n1=p2&space=p3&#=1", "string-1/40/10/100",
            new IIIFParsedNamedQuery(Customer)
            {
                String1 = "string-1", Number1 = 40, Space = 10,
                AssetOrdering = new List<ParsedNamedQuery.QueryOrder> { new(ParsedNamedQuery.QueryMapping.Number2) },
                NamedQueryName = "my-query"
            },
            "Extra args are ignored"
        },
        new object[]
        {
            "n3=&canvas=n2&=10&s1=p1&n1=p2&space=p3&#=1", "string-1/40",
            new IIIFParsedNamedQuery(Customer)
            {
                String1 = "string-1", Number1 = 40, Space = 1,
                AssetOrdering = new List<ParsedNamedQuery.QueryOrder> { new(ParsedNamedQuery.QueryMapping.Number2) },
                NamedQueryName = "my-query"
            },
            "Incorrect template pairs are ignored"
        },
        new object[]
        {
            "canvas=n2&s1=p1&n1=p2&batch=p3&manifest=p4&space=p5&#=1", "string-1/40/10,20,30/foo,bar",
            new IIIFParsedNamedQuery(Customer)
            {
                String1 = "string-1", Number1 = 40, Space = 1,
                AssetOrdering = new List<ParsedNamedQuery.QueryOrder> { new(ParsedNamedQuery.QueryMapping.Number2) },
                NamedQueryName = "my-query", Batches = [10, 20, 30], Manifests = ["foo", "bar"]
            },
            "All params including multi Batch and multi Manifest"
        }
    };
}
