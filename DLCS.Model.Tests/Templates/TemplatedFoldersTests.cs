using DLCS.Core.Types;
using DLCS.Model.Templates;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Templates
{
    public class TemplatedFoldersTests
    {
        [Fact]
        public void GenerateTemplate_ReturnsExpected_ImageNameUnaltered_IfImageName8CharsOrLess()
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var root = "folder";
            var asset = new AssetId(10, 20, "foobarba");
            var template = $"{s}{{root}}{s}{{customer}}{s}{{space}}{s}{{image}}";
            var expected = $"{s}folder{s}10{s}20{s}foobarba";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, root, asset);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GenerateTemplate_ReturnsExpected_ImageNameAltered_IfImageName8CharsOrLess()
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var root = "folder";
            var asset = new AssetId(10, 20, "foobarbazqux");
            var template = $"{s}{{root}}{s}{{customer}}{s}{{space}}{s}{{image}}";
            var expected = $"{s}folder{s}10{s}20{s}fo{s}ob{s}ar{s}ba{s}foobarbazqux";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, root, asset);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GenerateTemplate_ReturnsExpected_ImageNameNotReplaced_IfReplaceImageNameFalse()
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var root = "folder";
            var asset = new AssetId(10, 20, "foobarbazqux");
            var template = $"{s}{{root}}{s}{{customer}}{s}{{space}}{s}{{image}}";
            var expected = $"{s}folder{s}10{s}20{s}{{image}}";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, root, asset, false);

            // Assert
            result.Should().Be(expected);
        }
    }
}