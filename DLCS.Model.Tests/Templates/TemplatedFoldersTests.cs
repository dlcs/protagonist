using DLCS.Core.Types;
using DLCS.Model.Templates;
using FluentAssertions;
using Xunit;

namespace DLCS.Model.Tests.Templates
{
    public class TemplatedFoldersTests
    {
        [Theory]
        [InlineData("foobarba")]
        [InlineData("foobarbazqux")]
        public void GenerateTemplate_ReturnsExpected_ImageUnaltered_RegardlessOfSizeIfImageName8CharsOrLess(
            string imageName)
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var root = "folder";
            var asset = new AssetId(10, 20, imageName);
            var template = $"{s}{{root}}{s}{{customer}}{s}{{space}}{s}{{image}}";
            var expected = $"{s}folder{s}10{s}20{s}{imageName}";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, asset, root);

            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void GenerateTemplate_ReturnsExpected_ImageDirectoryUnaltered_IfImageName8CharsOrLess()
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var root = "folder";
            var asset = new AssetId(10, 20, "foobarba");
            var template = $"{s}{{root}}{s}{{customer}}{s}{{space}}{s}{{image-dir}}{s}{{image}}.ex";
            var expected = $"{s}folder{s}10{s}20{s}foobarba{s}foobarba.ex";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, asset, root);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void GenerateTemplate_ReturnsExpected_ImageDirectoryNameAltered_IfImageName8CharsOrLess()
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var root = "folder";
            var asset = new AssetId(10, 20, "foobarbazqux");
            var template = $"{s}{{root}}{s}{{customer}}{s}{{space}}{s}{{image-dir}}{s}{{image}}.ex";
            var expected = $"{s}folder{s}10{s}20{s}fo{s}ob{s}ar{s}ba{s}foobarbazqux{s}foobarbazqux.ex";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, asset, root);

            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public void GenerateTemplate_ReturnsExpected_WithoutRoot()
        {
            // Arrange
            char s = System.IO.Path.DirectorySeparatorChar;
            var asset = new AssetId(10, 20, "foobarbazqux");
            var template = $"{s}{{customer}}{s}{{space}}{s}{{image-dir}}{s}{{image}}.ex";
            var expected = $"{s}10{s}20{s}fo{s}ob{s}ar{s}ba{s}foobarbazqux{s}foobarbazqux.ex";

            // Act
            var result = TemplatedFolders.GenerateTemplate(template, asset);

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
            var result = TemplatedFolders.GenerateTemplate(template, asset, root, false);

            // Assert
            result.Should().Be(expected);
        }
    }
}