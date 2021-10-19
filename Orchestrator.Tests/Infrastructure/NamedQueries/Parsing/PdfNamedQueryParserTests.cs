using System;
using System.Collections.Generic;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Infrastructure.NamedQueries.Parsing;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.NamedQueries.Parsing
{
    public class PdfNamedQueryParserTests
    {
        private readonly PdfNamedQueryParser sut;
        private static readonly CustomerPathElement Customer = new(99, "test-customer");

        public PdfNamedQueryParserTests()
        {
            sut = new PdfNamedQueryParser(new NullLogger<PdfNamedQueryParser>());
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GenerateParsedNamedQueryFromRequest_Throws_IfTemplateEmptyOrWhiteSpace(string template)
        {
            // Act
            Action action = () =>
                sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, null, template);
            
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
            var result = sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template);

            // Assert
            result.IsFaulty.Should().BeTrue();
            result.ErrorMessage.Should().StartWith("Not enough query arguments to satisfy template element parameter");
        }

        [Theory]
        [InlineData("space=p1x", "1")]
        [InlineData("space=param", "1")]
        [InlineData("space=p1&s1=pa2&#=1", "")]
        public void GenerateParsedNamedQueryFromRequest_ReturnsFaultParsedNQ_IfInvalidParameterArgPassed(string template,
            string args)
        {
            // Act
            var result = sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template);

            // Assert
            result.IsFaulty.Should().BeTrue();
            result.ErrorMessage.Should().StartWith("Could not parse template element parameter");
        }
        
        [Theory]
        [InlineData("n1=p1", "not-an-int")]
        [InlineData("n1=p1&#=not-an-int", "")]
        public void GenerateParsedNamedQueryFromRequest_ReturnsFaultParsedNQ_IfNonNumberPassedForNumberArg(string template,
            string args)
        {
            // Act
            var result = sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template);

            // Assert
            result.IsFaulty.Should().BeTrue();
            result.ErrorMessage.Should().StartWith("Input string was not in a correct format");
        }
        
        [Theory]
        [MemberData(nameof(ParseNamedQueries))]
        public void GenerateParsedNamedQueryFromRequest_SuccessfullyParses(string template, string args,
            PdfParsedNamedQuery expected, string explanation)
        {
            // Act
            var result = sut.GenerateParsedNamedQueryFromRequest<PdfParsedNamedQuery>(Customer, args, template);
            
            // Assert
            result.Should().BeEquivalentTo(expected, explanation);
        }
        
        // Note: This is not a completely exhaustive list
        public static IEnumerable<object[]> ParseNamedQueries => new List<object[]>
        {
            new object[]
            {
                "space=p1", "10", new PdfParsedNamedQuery(Customer) { Space = 10, Args = new List<string> { "10" } },
                "Space from param"
            },
            new object[] { "space=5", "", new PdfParsedNamedQuery(Customer) { Space = 5 }, "Hardcoded value" },
            new object[]
            {
                "space=p1&#=10", "", new PdfParsedNamedQuery(Customer) { Space = 10, Args = new List<string> { "10" } },
                "Space from template"
            },
            new object[]
            {
                "spacename=p1", "10",
                new PdfParsedNamedQuery(Customer) { SpaceName = "10", Args = new List<string> { "10" } },
                "Spacename from param"
            },
            new object[]
            {
                "redactedmessage=you cannot view&canvas=s1&s1=p1&n1=p2&space=p3&#=1", "string-1/40",
                new PdfParsedNamedQuery(Customer)
                {
                    String1 = "string-1", Number1 = 40, Space = 1, RedactedMessage = "you cannot view",
                    Canvas = ParsedNamedQuery.QueryMapping.String1, Args = new List<string> { "string-1", "40", "1" }
                },
                "All params except format"
            },
            new object[]
            {
                "canvas=n2&s1=p1&n1=p2&space=p3&#=1", "string-1/40/10/100",
                new PdfParsedNamedQuery(Customer)
                {
                    String1 = "string-1", Number1 = 40, Space = 10, Canvas = ParsedNamedQuery.QueryMapping.Number2,
                    Args = new List<string> { "string-1", "40", "10", "100", "1" }
                },
                "Extra args are ignored"
            },
            new object[]
            {
                "manifest=s1&&n3=&canvas=n2&=10&s1=p1&n1=p2&space=p3&#=1", "string-1/40",
                new PdfParsedNamedQuery(Customer)
                {
                    String1 = "string-1", Number1 = 40, Space = 1, Canvas = ParsedNamedQuery.QueryMapping.Number2,
                    Args = new List<string> { "string-1", "40", "1" }
                },
                "Incorrect template pairs are ignored"
            },
            new object[]
            {
                "coverpage=https://{s3}&objectname={s3}_{n1}.pdf&n1=p1&s3=p2&#=foo", "10",
                new PdfParsedNamedQuery(Customer)
                {
                    String3 = "foo", Number1 = 10, CoverPageFormat = "https://{s3}", CoverPageUrl = "https://foo",
                    ObjectNameFormat = "{s3}_{n1}.pdf", ObjectName = "foo_10.pdf",
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
                    ObjectNameFormat = "{s3}_{n1}.pdf", ObjectName = "foo_10.pdf", Args = new List<string> { "10" }
                },
                "Replacements made from args and hardcoded template"
            },
            new object[]
            {
                "coverpage=https://{s3}&objectname={s3}_{n1}.pdf&s3=foo", "",
                new PdfParsedNamedQuery(Customer)
                {
                    String3 = "foo", CoverPageFormat = "https://{s3}", CoverPageUrl = "https://foo",
                    ObjectNameFormat = "{s3}_{n1}.pdf", ObjectName = "foo_.pdf"
                },
                "Replacements removed if no provided"
            },
        };
    }
}