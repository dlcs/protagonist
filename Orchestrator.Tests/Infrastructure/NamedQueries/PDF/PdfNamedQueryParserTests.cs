using System;
using System.Collections.Generic;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.PDF;
using Orchestrator.Settings;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.NamedQueries.PDF
{
    public class PdfNamedQueryParserTests
    {
        private readonly PdfNamedQueryParser sut;
        private static readonly CustomerPathElement Customer = new(99, "test-customer");

        public PdfNamedQueryParserTests()
        {
            var settings = Options.Create(new NamedQuerySettings());
            sut = new PdfNamedQueryParser(settings, new NullLogger<PdfNamedQueryParser>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GenerateParsedNamedQueryFromRequest_Throws_IfTemplateEmptyOrWhiteSpace(string template)
        {
            // Act
            Action action = () =>
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, null, template, "my-query");

            // Assert
            action.Should()
                .ThrowExactly<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'namedQueryTemplate')");
        }

        [Theory]
        [InlineData("space=p1", "")]
        [InlineData("space=p1&s1=p2", "1")]
        [InlineData("space=p1&s1=p2&#=1", "")]
        public void GenerateParsedNamedQueryFromRequest_ReturnsFaultParsedNQ_IfTooFewParamsPassed(string template,
            string args)
        {
            // Act
            var result =
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template, "my-query");

            // Assert
            result.IsFaulty.Should().BeTrue();
            result.ErrorMessage.Should().StartWith("Not enough query arguments to satisfy template element parameter");
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
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template, "my-query");

            // Assert
            result.IsFaulty.Should().BeTrue();
            result.ErrorMessage.Should().StartWith("Could not parse template element parameter");
        }

        [Theory]
        [InlineData("n1=p1", "not-an-int")]
        [InlineData("n1=p1&#=not-an-int", "")]
        public void GenerateParsedNamedQueryFromRequest_ReturnsFaultParsedNQ_IfNonNumberPassedForNumberArg(
            string template,
            string args)
        {
            // Act
            var result =
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template, "my-query");

            // Assert
            result.IsFaulty.Should().BeTrue();
            result.ErrorMessage.Should().StartWith("Input string was not in a correct format");
        }

        [Theory]
        [InlineData("n1=p1&n2=p2&s1=p3&#=foo", "1/2", "99/pdf/my-query/1/2/foo/Untitled")]
        [InlineData("n1=p1&n2=p2&s1=p3&#=foo", "1/2/3/4", "99/pdf/my-query/1/2/3/4/foo/Untitled")]
        [InlineData("n1=p1&n2=p2&objectname=file.pdf", "1/2", "99/pdf/my-query/1/2/file.pdf")]
        [InlineData("s1=p1&objectname=file_{s1}.pdf", "foo-bar", "99/pdf/my-query/foo-bar/file_foo-bar.pdf")]
        public void GenerateParsedNamedQueryFromRequest_SetsStorageKeys(string template, string args, string expected)
        {
            // Act
            var result =
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template, "my-query");

            // Assert
            result.StorageKey.Should().Be(expected);
            result.ControlFileStorageKey.Should().Be($"{expected}.json");
        }

        [Theory]
        [MemberData(nameof(ParseNamedQueries))]
        public void GenerateParsedNamedQueryFromRequest_SuccessfullyParses(string template, string args,
            PdfParsedNamedQuery expected, string explanation)
        {
            // Act
            var result =
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template, "my-query");

            // Assert
            result.Should().BeEquivalentTo(
                expected,
                opts => opts.Excluding(q => q.StorageKey).Excluding(q => q.ControlFileStorageKey),
                explanation);
        }

        // Note: This is not a completely exhaustive list
        public static IEnumerable<object[]> ParseNamedQueries => new List<object[]>
        {
            new object[]
            {
                "space=p1", "10",
                new PdfParsedNamedQuery(Customer)
                    { NamedQueryName = "my-query", Space = 10, Args = new List<string> { "10" } },
                "Space from param"
            },
            new object[]
            {
                "space=5", "", new PdfParsedNamedQuery(Customer) { NamedQueryName = "my-query", Space = 5 },
                "Hardcoded value"
            },
            new object[]
            {
                "space=p1&#=10", "",
                new PdfParsedNamedQuery(Customer)
                    { NamedQueryName = "my-query", Space = 10, Args = new List<string> { "10" } },
                "Space from template"
            },
            new object[]
            {
                "spacename=p1", "10",
                new PdfParsedNamedQuery(Customer)
                    { NamedQueryName = "my-query", SpaceName = "10", Args = new List<string> { "10" } },
                "Spacename from param"
            },
            new object[]
            {
                "redactedmessage=you cannot view&canvas=s1&s1=p1&n1=p2&space=p3&#=1", "string-1/40",
                new PdfParsedNamedQuery(Customer)
                {
                    String1 = "string-1", Number1 = 40, Space = 1, RedactedMessage = "you cannot view",
                    AssetOrdering = new List<ParsedNamedQuery.QueryOrder>{new(ParsedNamedQuery.QueryMapping.String1)}, 
                    Args = new List<string> { "string-1", "40", "1" }, NamedQueryName = "my-query",
                },
                "All params except format"
            },
            new object[]
            {
                "canvas=n2&s1=p1&n1=p2&space=p3&#=1", "string-1/40/10/100",
                new PdfParsedNamedQuery(Customer)
                {
                    String1 = "string-1", Number1 = 40, Space = 10,
                    AssetOrdering = new List<ParsedNamedQuery.QueryOrder>{new(ParsedNamedQuery.QueryMapping.Number2)},
                    Args = new List<string> { "string-1", "40", "10", "100", "1" }, NamedQueryName = "my-query",
                },
                "Extra args are ignored"
            },
            new object[]
            {
                "manifest=s1&&n3=&canvas=n2&=10&s1=p1&n1=p2&space=p3&#=1", "string-1/40",
                new PdfParsedNamedQuery(Customer)
                {
                    String1 = "string-1", Number1 = 40, Space = 1, 
                    AssetOrdering = new List<ParsedNamedQuery.QueryOrder>{new(ParsedNamedQuery.QueryMapping.Number2)},
                    Args = new List<string> { "string-1", "40", "1" }, NamedQueryName = "my-query",
                },
                "Incorrect template pairs are ignored"
            },
            new object[]
            {
                "coverpage=https://{s3}&objectname={s3}_{n1}.pdf&n1=p1&s3=p2&#=foo", "10",
                new PdfParsedNamedQuery(Customer)
                {
                    String3 = "foo", Number1 = 10, CoverPageFormat = "https://{s3}", CoverPageUrl = "https://foo",
                    ObjectNameFormat = "{s3}_{n1}.pdf", ObjectName = "foo_10.pdf", NamedQueryName = "my-query",
                    Args = new List<string> { "10", "foo" }
                },
                "Replacements made from args and template"
            },
            new object[]
            {
                "coverpage=https://{s3}&objectname={s3}_{n1}.pdf&n1=p1&s3=foo", "10",
                new PdfParsedNamedQuery(Customer)
                {
                    String3 = "foo", Number1 = 10, CoverPageFormat = "https://{s3}", CoverPageUrl = "https://foo",
                    ObjectNameFormat = "{s3}_{n1}.pdf", ObjectName = "foo_10.pdf", Args = new List<string> { "10" },
                    NamedQueryName = "my-query",
                },
                "Replacements made from args and hardcoded template"
            },
            new object[]
            {
                "coverpage=https://{s3}&objectname={s3}_{n1}.pdf&s3=foo", "",
                new PdfParsedNamedQuery(Customer)
                {
                    String3 = "foo", CoverPageFormat = "https://{s3}", CoverPageUrl = "https://foo",
                    ObjectNameFormat = "{s3}_{n1}.pdf", ObjectName = "foo_.pdf", NamedQueryName = "my-query",
                },
                "Replacements removed if no provided"
            },
            new object[]
            {
                "assetOrder=n2;s1 desc", "",
                new PdfParsedNamedQuery(Customer)
                {
                    AssetOrdering = new List<ParsedNamedQuery.QueryOrder>
                    {
                        new(ParsedNamedQuery.QueryMapping.Number2),
                        new(ParsedNamedQuery.QueryMapping.String1, ParsedNamedQuery.OrderDirection.Descending)
                    },
                    NamedQueryName = "my-query",
                },
                "Asset Ordering"
            },
        };
    }
}