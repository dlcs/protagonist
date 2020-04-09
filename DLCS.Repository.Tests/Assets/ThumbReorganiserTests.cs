using System.Collections.Generic;
using System.Linq;
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
        private readonly IBucketReader bucketReader;
        private readonly ILogger<ThumbRepository> logger;
        private readonly IAssetRepository assetRepository;
        private readonly IThumbnailPolicyRepository thumbPolicyRepository;
        private readonly ThumbReorganiser sut;

        public ThumbReorganiserTests()
        {
            bucketReader = A.Fake<IBucketReader>();
            logger = A.Fake<ILogger<ThumbRepository>>();
            assetRepository = A.Fake<IAssetRepository>();
            thumbPolicyRepository = A.Fake<IThumbnailPolicyRepository>();
            sut = new ThumbReorganiser(bucketReader, logger, assetRepository, thumbPolicyRepository);
        }

        [Fact]
        public async Task EnsureNewLayout_DoesNothing_IfSizesJsonExists()
        {
            // Arrange
            var rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[] {"2/1/the-astronaut/s.json", "2/1/the-astronaut/200.jpg"});
            
            // Act
            await sut.EnsureNewLayout(rootKey);
            
            // Assert
            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .MustNotHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_CreatesExpectedResources_AllOpen()
        {
            var rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[] {"2/1/the-astronaut/200.jpg"});

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 4000, Height = 8000, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbPolicyRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "400,200,100"});
            
            // Act
            await sut.EnsureNewLayout(rootKey);

            // Assert
            
            // move jpg per thumbnail size
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", 
                        "2/1/the-astronaut/low.jpg",
                        "2/1/the-astronaut/open/400.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket",
                        "2/1/the-astronaut/full/100,200/0/default.jpg",
                        "2/1/the-astronaut/open/200.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", 
                        "2/1/the-astronaut/full/50,100/0/default.jpg",
                        "2/1/the-astronaut/open/100.jpg"))
                .MustHaveHappened();
            
            // create sizes.json
            const string expected = "{\"o\":[[200,400],[100,200],[50,100]],\"a\":[]}";
            A.CallTo(() =>
                    bucketReader.WriteToBucket(
                        A<ObjectInBucket>.That.Matches(o =>
                            o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                        "application/json"))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_CreatesExpectedResources_AllAuth()
        {
            var rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[] {"2/1/the-astronaut/200.jpg"});

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 2000, Height = 4000, ThumbnailPolicy = "TheBestOne", MaxUnauthorised = 0, Roles = "admin"});
            A.CallTo(() => thumbPolicyRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "400,200,100"});
            
            // Act
            await sut.EnsureNewLayout(rootKey);

            // Assert
            
            // move jpg per thumbnail size
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", 
                        "2/1/the-astronaut/low.jpg",
                        "2/1/the-astronaut/auth/400.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket",
                        "2/1/the-astronaut/full/100,200/0/default.jpg",
                        "2/1/the-astronaut/auth/200.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", 
                        "2/1/the-astronaut/full/50,100/0/default.jpg",
                        "2/1/the-astronaut/auth/100.jpg"))
                .MustHaveHappened();
            
            // create sizes.json
            const string expected = "{\"o\":[],\"a\":[[200,400],[100,200],[50,100]]}";
            A.CallTo(() =>
                    bucketReader.WriteToBucket(
                        A<ObjectInBucket>.That.Matches(o =>
                            o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                        "application/json"))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_CreatesExpectedResources_MixedAuthAndOpen()
        {
            var rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[] {"2/1/the-astronaut/200.jpg"});

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 2000, Height = 4000, ThumbnailPolicy = "TheBestOne", MaxUnauthorised = 350, Roles = "admin"});
            A.CallTo(() => thumbPolicyRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "1024,400,200,100"});
            
            // Act
            await sut.EnsureNewLayout(rootKey);

            // Assert
            
            // move jpg per thumbnail size
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket",
                        "2/1/the-astronaut/low.jpg",
                        "2/1/the-astronaut/auth/1024.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", 
                        "2/1/the-astronaut/full/200,400/0/default.jpg",
                        "2/1/the-astronaut/auth/400.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket",
                        "2/1/the-astronaut/full/100,200/0/default.jpg",
                        "2/1/the-astronaut/open/200.jpg"))
                .MustHaveHappened();
            A.CallTo(() =>
                    bucketReader.CopyWithinBucket("the-bucket", 
                        "2/1/the-astronaut/full/50,100/0/default.jpg",
                        "2/1/the-astronaut/open/100.jpg"))
                .MustHaveHappened();
            
            // create sizes.json
            const string expected = "{\"o\":[[100,200],[50,100]],\"a\":[[512,1024],[200,400]]}";
            A.CallTo(() =>
                    bucketReader.WriteToBucket(
                        A<ObjectInBucket>.That.Matches(o =>
                            o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                        "application/json"))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_DeletesOldConfinedSquareLayout()
        {
            var rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .Returns(new[]
                {
                    "2/1/the-astronaut/low.jpg", "2/1/the-astronaut/100.jpg", "2/1/the-astronaut/sizes.json",
                    "2/1/the-astronaut/full/50,100/0/default.jpg"
                });

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 4000, Height = 8000, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbPolicyRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "200,100"});
            
            // Act
            await sut.EnsureNewLayout(rootKey);

            // Assert
            var expectedDeletions = new[]
            {
                "the-bucket:::2/1/the-astronaut/100.jpg", "the-bucket:::2/1/the-astronaut/sizes.json"
            };

            A.CallTo(() => bucketReader.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
                expectedDeletions.Contains(a[0].ToString()) && expectedDeletions.Contains(a[1].ToString())
            ))).MustHaveHappened();
        }
        
        [Fact]
        public async Task EnsureNewLayout_DoesNotMakeConcurrentAttempts_ForSameKey()
        {
            var rootKey = new ObjectInBucket {Bucket = "the-bucket", Key = "2/1/the-astronaut/"};
            var fakeBucketContents = new List<string> {"2/1/the-astronaut/200.jpg"};

            A.CallTo(() => bucketReader.GetMatchingKeys(rootKey))
                .ReturnsLazily(() =>  fakeBucketContents.ToArray());

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 200, Height = 250, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbPolicyRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "400,200,100"});
            
            // Once called, add sizes.json to return list of bucket contents
            A.CallTo(() => bucketReader.WriteToBucket(A<ObjectInBucket>._, A<string>._, A<string>._))
                .Invokes(() => fakeBucketContents.Add("2/1/the-astronaut/s.json"));

            A.CallTo(() => bucketReader.CopyWithinBucket(A<string>._, A<string>._, A<string>._))
                .Invokes(async () => await Task.Delay(500));

            var ensure1 = Task.Factory.StartNew(() => sut.EnsureNewLayout(rootKey));
            var ensure2 = Task.Factory.StartNew(() => sut.EnsureNewLayout(rootKey));

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

            A.CallTo(() => bucketReader.GetMatchingKeys(key1))
                .ReturnsLazily(() =>  fakeBucketContents.ToArray());

            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .Returns(new Asset {Width = 200, Height = 250, ThumbnailPolicy = "TheBestOne"});
            A.CallTo(() => thumbPolicyRepository.GetThumbnailPolicy("TheBestOne"))
                .Returns(new ThumbnailPolicy {Sizes = "400,200,100"});
            
            // Once called, add sizes.json to return list of bucket contents
            A.CallTo(() => bucketReader.WriteToBucket(A<ObjectInBucket>._, A<string>._, A<string>._))
                .Invokes((ObjectInBucket dest, string content, string contentType) =>
                    fakeBucketContents.Add(dest.Key + "sizes.json"));

            A.CallTo(() => bucketReader.CopyWithinBucket(A<string>._, A<string>._, A<string>._))
                .Invokes(async () => await Task.Delay(500));

            var ensure1 = Task.Factory.StartNew(() => sut.EnsureNewLayout(key1));
            var ensure2 = Task.Factory.StartNew(() => sut.EnsureNewLayout(key2));
            var ensure3 = Task.Factory.StartNew(() => sut.EnsureNewLayout(key3));

            // Act
            await Task.WhenAll(ensure1, ensure2, ensure3);
            
            // Assert
            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                .MustHaveHappened(3, Times.Exactly);
        }

        [Fact]
        public async Task EnsureNewLayout_AssetNotFound()
        {

            // Arrange
            var rootKey = new ObjectInBucket { Bucket = "the-bucket", Key = "2/1/doesnotexit/" };

            Asset returnvalue = null;
            A.CallTo(() => assetRepository.GetAsset(rootKey.Key.TrimEnd('/')))
                     .Returns(returnvalue);
           
            // Act
            await sut.EnsureNewLayout(rootKey);

            // Assert
            A.CallTo(() => assetRepository.GetAsset(A<string>._))
                  .MustHaveHappened();

        }
    }
}