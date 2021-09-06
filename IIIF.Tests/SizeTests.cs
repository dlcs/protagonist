using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace IIIF.Tests
{
    public class SizeTests
    {
        [Fact]
        public void FromArray_ReturnsCorrectSize()
        {
            // Arrange
            const int width = 1234;
            const int height = 8876;
            var widthHeight = new[] {width, height};

            // Act
            var size = Size.FromArray(widthHeight);
            
            // Assert
            size.Width.Should().Be(width);
            size.Height.Should().Be(height);
        }

        [Fact]
        public void FromString_ReturnsCorrect_WH()
        {
            // Arrange
            const string actual = "100,200";
            
            // Act
            var size = Size.FromString(actual);
            
            // Assert
            size.Width.Should().Be(100);
            size.Height.Should().Be(200);
        }

        [Fact]
        public void ToArray_ReturnsWidthHeight()
        {
            // Arrange
            const int width = 1234;
            const int height = 8876;
            var expected = new[] {width, height};
            var size = new Size(width, height);

            // Act
            var result = size.ToArray();
            
            // Assert
            result.Should().BeEquivalentTo(expected);
        }
        
        [Fact]
        public void ToString_ReturnsWidthHeight()
        {
            // Arrange
            const int width = 1234;
            const int height = 8876;
            var expected = $"{width},{height}";
            var size = new Size(width, height);

            // Act
            var result = size.ToString();
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Theory, MemberData(nameof(ConfineDataSquare))]
        public void ConfineStaticMethod_BoundingSize_ReturnsCorrectSize(TestSizeData testData)
        {
            // Arrange
            var size = new Size(testData.CurrentWidth, testData.CurrentHeight);
            
            // Act
            var confined = Size.Confine(testData.ConfineWidth, size);
            
            // Assert
            confined.Width.Should().Be(testData.ExpectedWidth);
            confined.Height.Should().Be(testData.ExpectedHeight);
        }
        
        [Theory, MemberData(nameof(ConfineData))]
        public void ConfineStaticMethod_ReturnsCorrectSize(TestSizeData testData)
        {
            // Arrange
            var size = new Size(testData.CurrentWidth, testData.CurrentHeight);
            var requiredSize = new Size(testData.ConfineWidth, testData.ConfineHeight);
            
            // Act
            var confined = Size.Confine(requiredSize, size);
            
            // Assert
            confined.Width.Should().Be(testData.ExpectedWidth);
            confined.Height.Should().Be(testData.ExpectedHeight);
        }

        [Fact]
        public void Resize_Throws_IfHeightAndWidthNull()
        {
            // Arrange
            var size = new Size(100, 100);
            
            // Act
            Action action = () => Size.Resize(size, null, null);
            
            // Assert
            action.Should().Throw<InvalidOperationException>();
        }
        
        [Fact]
        public void Resize_ReturnsCorrectSize_IfHeightAndWidthSpecified()
        {
            // Arrange
            var size = new Size(100, 100);
            
            // Act
            var newSize = Size.Resize(size, 90, 400);
            
            // Assert
            newSize.Width.Should().Be(90);
            newSize.Height.Should().Be(400);
        }
        
        [Theory]
        [InlineData(400, null)]
        [InlineData(null, 400)]
        public void Resize_ReturnsNewSize_Square(int? width, int? height)
        {
            // Arrange
            var size = new Size(100, 100);
            
            // Act
            var newSize = Size.Resize(size, width, height);
            
            // Assert
            newSize.Width.Should().Be(width ?? height);
            newSize.Height.Should().Be(width ?? height);
        }

        [Theory]
        [InlineData(400, 200)]
        [InlineData(50, 25)]
        public void Resize_ReturnsCorrectSize_Landscape_FromWidth(int width, int expectedHeight)
        {
            // Arrange
            var size = new Size(200, 100);
            
            // Act
            var newSize = Size.Resize(size, width);
            
            // Assert
            newSize.Width.Should().Be(width);
            newSize.Height.Should().Be(expectedHeight);
        }
        
        [Theory]
        [InlineData(200, 400)]
        [InlineData(25, 50)]
        public void Resize_ReturnsCorrectSize_Landscape_FromHeight(int height, int expectedWidth)
        {
            // Arrange
            var size = new Size(200, 100);
            
            // Act
            var newSize = Size.Resize(size, targetHeight: height);
            
            // Assert
            newSize.Height.Should().Be(height);
            newSize.Width.Should().Be(expectedWidth);
        }

        [Theory]
        [InlineData(200, 400)]
        [InlineData(25, 50)]
        public void Resize_ReturnsCorrectSize_Portrait_FromWidth(int width, int expectedHeight)
        {
            // Arrange
            var size = new Size(100, 200);
            
            // Act
            var newSize = Size.Resize(size, width);
            
            // Assert
            newSize.Width.Should().Be(width);
            newSize.Height.Should().Be(expectedHeight);
        }
        
        [Theory]
        [InlineData(400, 200)]
        [InlineData(50, 25)]
        public void Resize_ReturnsCorrectSize_Portrait_FromHeight(int height, int expectedWidth)
        {
            // Arrange
            var size = new Size(100, 200);
            
            // Act
            var newSize = Size.Resize(size, targetHeight: height);
            
            // Assert
            newSize.Height.Should().Be(height);
            newSize.Width.Should().Be(expectedWidth);
        }

        [Theory]
        [InlineData(100, 100, 50, 10, 10, 10)] // shrink square to landscape
        [InlineData(100, 100, 10, 50, 10, 10)] // shrink square to portrait
        [InlineData(100, 100, 50, 50, 50, 50)] // shrink square to square
        [InlineData(100, 100, 500, 600, 500, 500)] // grow square to landscape
        [InlineData(100, 100, 500, 300, 300, 300)] // grow square to portrait
        [InlineData(100, 100, 500, 500, 500, 500)] // grow square to square
        [InlineData(200, 100, 50, 10, 20, 10)] // shrink landscape to landscape
        [InlineData(200, 100, 10, 50, 10, 5)] // shrink landscape to portrait
        [InlineData(200, 100, 50, 50, 50, 25)] // shrink landscape to square
        [InlineData(200, 100, 500, 300, 500, 250)] // grow landscape to landscape
        [InlineData(200, 100, 500, 600, 500, 250)] // grow landscape to portrait
        [InlineData(200, 100, 500, 500, 500, 250)] // grow landscape to square
        [InlineData(100, 200, 50, 10, 5, 10)] // shrink portrait to landscape
        [InlineData(100, 200, 10, 50, 10, 20)] // shrink portrait to portrait
        [InlineData(100, 200, 50, 50, 25, 50)] // shrink portrait to square
        [InlineData(100, 200, 500, 300, 150, 300)] // grow portrait to landscape
        [InlineData(100, 200, 500, 600, 300, 600)] // grow portrait to portrait
        [InlineData(100, 200, 500, 500, 250, 500)] // grow portrait to square
        public void FitWithin_Returns_CorrectSize(int w, int h, int targetWidth, int targetHeight, int expectedWidth, int expectedHeight)
        {
            // Arrange 
            var size = new Size(w, h);

            // Act
            var newSize = Size.FitWithin(new Size(targetWidth, targetHeight), size);
            
            // Assert
            newSize.Height.Should().Be(expectedHeight);
            newSize.Width.Should().Be(expectedWidth);
        }
        
        [Theory]
        [InlineData(10, 10, 10, 10)] // square same size
        [InlineData(10, 5, 10, 5)] // landscape same size
        [InlineData(5, 10, 5, 10)] // portrait same size
        [InlineData(10, 5, 10, 10)] // landscape within 
        [InlineData(5, 10, 10, 10)] // portrait within
        [InlineData(5, 5, 10, 10)] // both dimensions within
        public void IsConfinedWithin_True_IfConfinedWithin(int width, int height, int confinedWidth, int confinedHeight)
        {
            // Arrange
            var size = new Size(width, height);
            var confineSize = new Size(confinedWidth, confinedHeight);

            // Assert
            size.IsConfinedWithin(confineSize).Should().BeTrue();
        }
        
        [Theory]
        [InlineData(11, 11, 10, 10)] // both dimensions out
        [InlineData(11, 9, 10, 10)] // width outside, height inside
        [InlineData(9, 11, 10, 10)] // height outside, width inside
        public void IsConfinedWithin_False_IfNotConfinedWithin(int width, int height, int confinedWidth, int confinedHeight)
        {
            // Arrange
            var size = new Size(width, height);
            var confineSize = new Size(confinedWidth, confinedHeight);

            // Assert
            size.IsConfinedWithin(confineSize).Should().BeFalse();
        }

        [Theory]
        [InlineData(10, 10, 10)]
        [InlineData(5, 10, 10)]
        [InlineData(10, 5, 10)]
        public void MaxDimension_Correct(int w, int h, int expected)
            => new Size(w, h).MaxDimension.Should().Be(expected);
        
        [Theory]
        [InlineData(10, 10, ImageShape.Square)]
        [InlineData(5, 10, ImageShape.Portrait)]
        [InlineData(10, 5, ImageShape.Landscape)]
        public void GetShape_Correct(int w, int h, ImageShape expected)
            => new Size(w, h).GetShape().Should().Be(expected);

        [Fact]
        public void GetSizeIncreasePercent_Correct_SameSize()
        {
            // Arrange
            var large = new Size(100, 200);
            var small = new Size(100, 200);
            
            // Act
            var difference = Size.GetSizeIncreasePercent(large, small);
            
            // Assert
            difference.Should().Be(0);
        }
        
        [Fact]
        public void GetSizeIncreasePercent_Correct()
        {
            // Arrange
            var large = new Size(300, 400);
            var small = new Size(100, 200);
            
            // Act
            var difference = Size.GetSizeIncreasePercent(large, small);
            
            // Assert
            difference.Should().Be(100);
        }
        
        [Fact]
        public void GetSizeIncreasePercent_Throws_IfSmallerLarger()
        {
            // Arrange
            var large = new Size(300, 400);
            var small = new Size(100, 800);
            
            // Act
            Action action = () => Size.GetSizeIncreasePercent(large, small);
            
            // Assert
            action.Should().Throw<InvalidOperationException>();
        }

        private static List<TestSizeData> sampleTestData = new()
        {
            // currW, currH, confW, confH, expectedW, expectedH
            new(200, 200, 300, 300, 200, 200), // actual smaller than confine
            new(500, 500, 300, 300, 300, 300), // confined square
            new(500, 400, 300, 300, 300, 240), // current portrait
            new(400, 500, 300, 300, 240, 300), // current landscape
            new(500, 500, 300, 200, 200, 200), // target portrait
            new(500, 500, 200, 300, 200, 300), // target landscape
            new(4553, 5668, 200, 200, 161, 200), // a specific rounding issue with Appetiser
        };
        
        // Test data for square confine targets only
        public static IEnumerable<object[]> ConfineDataSquare
        {
            get
            {
                var data = sampleTestData.Where(d => d.ConfineWidth == d.ConfineHeight);

                var retVal = new List<object[]>();
                retVal.AddRange(data.Select(sizeData => new object[] {sizeData}));
                return retVal;
            }
        }
        
        // Test data for square and non-square confine targets
        public static IEnumerable<object[]> ConfineData
        {
            get
            {
                var data = sampleTestData.Where(d => d.ConfineWidth == d.ConfineHeight);

                var retVal = new List<object[]>();
                retVal.AddRange(data.Select(sizeData => new object[] {sizeData}));
                return retVal;
            }
        }

        public class TestSizeData
        {
            public int CurrentWidth { get; }
            public int CurrentHeight { get; }
            public int ConfineWidth { get; }
            public int ConfineHeight { get; }
            public int ExpectedWidth { get; }
            public int ExpectedHeight { get; }

            public TestSizeData(int currentWidth, int currentHeight, int confineWidth, int confineHeight,
                int expectedWidth, int expectedHeight)
            {
                CurrentWidth = currentWidth;
                CurrentHeight = currentHeight;
                ConfineWidth = confineWidth;
                ConfineHeight = confineHeight;
                ExpectedWidth = expectedWidth;
                ExpectedHeight = expectedHeight;
            }
        }
    }
}
