using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Storage;
using DLCS.Repository.Assets;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DLCS.Repository.Tests.Assets
{
    public class ThumbReorganiserTests
    {
        private readonly ObjectInBucket rootKey;
        private readonly IBucketReader bucketReader;
        private readonly ILogger<ThumbRepository> logger;
        private readonly IAssetRepository assetRepository;
        private readonly IThumbRepository thumbRepository;

        public ThumbReorganiserTests()
        {
            rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            bucketReader = A.Fake<IBucketReader>();
            logger = A.Fake<ILogger<ThumbRepository>>();
            assetRepository = A.Fake<IAssetRepository>();
            thumbRepository = A.Fake<IThumbRepository>();
        }

        [Fact]
        public async Task EnsureNewLayout_DoesNothing_IfSizesJsonExists()
        {
            // Arrange
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[] {"2/1/the-astronaut/sizes.json", "2/1/the-astronaut/200.jpg"});
            
            var sut = GetThumbReorganiser();
            
            // Act
            await sut.EnsureNewLayout();
            
            // Assert
            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .MustNotHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_CreatesExpectedResources()
        {
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[] {"2/1/the-astronaut/200.jpg"});

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 200, Height = 400, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "100,200,400"});

            var sut = GetThumbReorganiser();
            
            // Act
            await sut.EnsureNewLayout();

            // Assert
            
            // move jpg per thumbnail size
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", "2/1/the-astronaut/low.jpg", "2/1/the-astronaut/400.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", "2/1/the-astronaut/full/100,200/0/default.jpg", "2/1/the-astronaut/200.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", "2/1/the-astronaut/full/50,100/0/default.jpg", "2/1/the-astronaut/100.jpg"))
                .MustHaveHappened();
            
            // create sizes.json
            A.CallTo(() =>
                    bucketReader.WriteToBucket(
                        A<ObjectInBucket>.That.Matches(o =>
                            o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/sizes.json"), A<string>._,
                        "application/json"))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_DoesNotMakeConcurrentAttempts_ForSameKey()
        {
            var fakeBucketContents = new List<string> {"2/1/the-astronaut/200.jpg"};

            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .ReturnsLazily(() =>  fakeBucketContents.ToArray());

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 200, Height = 250, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "100,200,400"});
            
            // Once called, add sizes.json to return list of bucket contents
            A.CallTo(() => bucketReader.WriteToBucket(A<ObjectInBucket>._, A<string>._, A<string>._))
                .Invokes(() => fakeBucketContents.Add("2/1/the-astronaut/sizes.json"));

            A.CallTo(() => bucketReader.CopyWithinBucket(A<string>._, A<string>._, A<string>._))
                .Invokes(async () => await Task.Delay(500));

            var sut = GetThumbReorganiser();
            var sut2 = GetThumbReorganiser();
            var ensure1 = Task.Factory.StartNew(() => sut.EnsureNewLayout());
            var ensure2 = Task.Factory.StartNew(() => sut2.EnsureNewLayout());

            // Act
            await Task.WhenAll(ensure1, ensure2);
            
            // Assert
            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .MustHaveHappenedOnceExactly();
        }
        
        [Fact]
        public async Task EnsureNewLayout_AllowsConcurrentAttempts_ForDifferentKey()
        {
            var key1 = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            var key2 = new ObjectInBucket {Bucket = "another-bucket", Key = "2/1/the-astronaut/"};
            var key3 = new ObjectInBucket {Bucket = "the-bucket", Key = "3/1/the-astronaut/"};
            
            var fakeBucketContents = new List<string> {"2/1/the-astronaut/200.jpg"};

            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .ReturnsLazily(() =>  fakeBucketContents.ToArray());

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 200, Height = 250, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "100,200,400"});
            
            // Once called, add sizes.json to return list of bucket contents
            A.CallTo(() => bucketReader.WriteToBucket(A<ObjectInBucket>._, A<string>._, A<string>._))
                .Invokes((ObjectInBucket dest, string content, string contentType) =>
                    fakeBucketContents.Add(dest.Key + "sizes.json"));

            A.CallTo(() => bucketReader.CopyWithinBucket(A<string>._, A<string>._, A<string>._))
                .Invokes(async () => await Task.Delay(500));

            var sut = GetThumbReorganiser(key1);
            var sut2 = GetThumbReorganiser(key2);
            var sut3 = GetThumbReorganiser(key3);
            var ensure1 = Task.Factory.StartNew(() => sut.EnsureNewLayout());
            var ensure2 = Task.Factory.StartNew(() => sut2.EnsureNewLayout());
            var ensure3 = Task.Factory.StartNew(() => sut3.EnsureNewLayout());

            // Act
            await Task.WhenAll(ensure1, ensure2, ensure3);
            
            // Assert
            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .MustHaveHappened(3, Times.Exactly);
        }

        private ThumbReorganiser GetThumbReorganiser(ObjectInBucket? objectInBucket = null) =>
            new ThumbReorganiser(objectInBucket ?? rootKey, bucketReader, logger, assetRepository, thumbRepository);
    }
}