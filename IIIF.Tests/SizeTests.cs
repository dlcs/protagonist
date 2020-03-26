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

        private static List<TestSizeData> sampleTestData = new List<TestSizeData>
        {
            new TestSizeData(200, 200, 300, 300, 200, 200), // actual smaller than confine
            new TestSizeData(500, 500, 300, 300, 300, 300), // confined square
            new TestSizeData(500, 400, 300, 300, 300, 240), // current portrait
            new TestSizeData(400, 500, 300, 300, 240, 300), // current landscape
            new TestSizeData(500, 500, 300, 200, 200, 200), // target portrait
            new TestSizeData(500, 500, 200, 300, 200, 300), // target landscape
            new TestSizeData(4553, 5668, 200, 200, 161, 200), // a specific rounding issue with Appetiser
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
