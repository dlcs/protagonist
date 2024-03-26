using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Thumbs;
using DLCS.Model.Policies;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thumbs.Reorganising;

namespace Thumbs.Tests.Reorganising;

public class ThumbReorganiserTests
{
    private readonly IBucketReader bucketReader;
    private readonly IAssetRepository assetRepository;
    private readonly IPolicyRepository thumbPolicyRepository;
    private readonly ThumbReorganiser sut;
    private readonly IBucketWriter bucketWriter;
    private readonly List<ImageDeliveryChannel> imageDeliveryChannels;
    
    public ThumbReorganiserTests()
    {
        bucketReader = A.Fake<IBucketReader>();
        bucketWriter = A.Fake<IBucketWriter>();
        assetRepository = A.Fake<IAssetRepository>();
        thumbPolicyRepository = A.Fake<IPolicyRepository>();
        IStorageKeyGenerator storageKeyGenerator = new S3StorageKeyGenerator(
            Options.Create(new AWSSettings { S3 = new S3Settings { ThumbsBucket = "the-bucket" } }));
        sut = new ThumbReorganiser(bucketReader, bucketWriter, new NullLogger<ThumbReorganiser>(), assetRepository,
            thumbPolicyRepository, storageKeyGenerator);

        imageDeliveryChannels = new List<ImageDeliveryChannel>
        {
            new()
            {
                Channel = AssetDeliveryChannels.Image,
                DeliveryChannelPolicyId = 1,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    Name = "default",
                    Channel = AssetDeliveryChannels.Image
                }
            },
            new()
            {
                Channel = AssetDeliveryChannels.Thumbnails,
                DeliveryChannelPolicyId = 2,
                DeliveryChannelPolicy = new DeliveryChannelPolicy()
                {
                    Name = "default",
                    PolicyData = "[\"!1024,1024\",\"!400,400\",\"!200,200\",\"!100,100\"]",
                    Channel = AssetDeliveryChannels.Thumbnails
                }
            }
        };
    }

    [Fact]
    public async Task EnsureNewLayout_DoesNothing_IfSizesJsonExists()
    {
        // Arrange
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() =>
                bucketReader.GetMatchingKeys(
                    A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[] { "2/1/the-astronaut/s.json", "2/1/the-astronaut/200.jpg" });
        
        // Act
        var response = await sut.EnsureNewLayout(assetId);
        
        // Assert
        response.Should().Be(ReorganiseResult.HasExpectedLayout);
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_AllOpen()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() =>
                bucketReader.GetMatchingKeys(
                    A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/100,/0/default.jpg",
                "2/1/the-astronaut/full/100,200/0/default.jpg",
                "2/1/the-astronaut/full/50,/0/default.jpg",
                "2/1/the-astronaut/full/50,100/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset 
            {
                Width = 4000,
                Height = 8000,
                MaxUnauthorised = -1,
                ImageDeliveryChannels = imageDeliveryChannels
                
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);
        
        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/100,200/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/200.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/100.jpg")))
            .MustHaveHappened();
        
        // create sizes.json
        const string expected = "{\"o\":[[512,1024],[200,400],[100,200],[50,100]],\"a\":[]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_AllAuthDueToMaxUnauthorised()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() =>
                bucketReader.GetMatchingKeys(
                    A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/100,/0/default.jpg",
                "2/1/the-astronaut/full/100,200/0/default.jpg",
                "2/1/the-astronaut/full/50,/0/default.jpg",
                "2/1/the-astronaut/full/50,100/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset 
            {
                Width = 4000, 
                Height = 8000,
                MaxUnauthorised = 0, 
                ImageDeliveryChannels = imageDeliveryChannels
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);
        
        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/100,200/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/200.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/100.jpg")))
            .MustHaveHappened();
        
        // create sizes.json
        const string expected = "{\"o\":[],\"a\":[[512,1024],[200,400],[100,200],[50,100]]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_AllAuth()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/100,/0/default.jpg",
                "2/1/the-astronaut/full/100,200/0/default.jpg",
                "2/1/the-astronaut/full/50,/0/default.jpg",
                "2/1/the-astronaut/full/50,100/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset
            {
                Width = 2000, 
                Height = 4000, 
                MaxUnauthorised = 0, 
                Roles = "admin", 
                ImageDeliveryChannels = imageDeliveryChannels
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);

        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/100,200/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/200.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/100.jpg")))
            .MustHaveHappened();
        
        // create sizes.json
        const string expected = "{\"o\":[],\"a\":[[512,1024],[200,400],[100,200],[50,100]]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_MixedAuthAndOpen()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/200,/0/default.jpg",
                "2/1/the-astronaut/full/200,400/0/default.jpg",
                "2/1/the-astronaut/full/100,/0/default.jpg",
                "2/1/the-astronaut/full/100,200/0/default.jpg",
                "2/1/the-astronaut/full/50,/0/default.jpg",
                "2/1/the-astronaut/full/50,100/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset {
                Width = 2000, 
                Height = 4000, 
                MaxUnauthorised = 350, 
                Roles = "admin", 
                ImageDeliveryChannels = imageDeliveryChannels
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);

        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/200,400/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/400.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/100,200/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/200.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/100.jpg")))
            .MustHaveHappened();
        
        // create sizes.json
        const string expected = "{\"o\":[[100,200],[50,100]],\"a\":[[512,1024],[200,400]]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_HandlingRoundingDifference_Portrait()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/201,/0/default.jpg",
                "2/1/the-astronaut/full/201,400/0/default.jpg",
                "2/1/the-astronaut/full/99,/0/default.jpg",
                "2/1/the-astronaut/full/99,200/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset {
                Width = 2000, 
                Height = 4000, 
                MaxUnauthorised = 350, 
                Roles = "admin", 
                ImageDeliveryChannels = imageDeliveryChannels
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);

        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/201,400/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/400.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/99,200/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/200.jpg")))
            .MustHaveHappened();
        // this shouldn't happen as matching key not found
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>._))
            .MustNotHaveHappened();
        
        // create sizes.json
        const string expected = "{\"o\":[[100,200],[50,100]],\"a\":[[512,1024],[200,400]]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_HandlingRoundingDifference_Landscape()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/400,/0/default.jpg",
                "2/1/the-astronaut/full/400,201/0/default.jpg",
                "2/1/the-astronaut/full/200,/0/default.jpg",
                "2/1/the-astronaut/full/200,99/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset
            {
                Width = 2000, 
                Height = 4000, 
                MaxUnauthorised = 350, 
                Roles = "admin", 
                ImageDeliveryChannels = imageDeliveryChannels
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);
        
        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/400,201/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/400.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/200,99/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/200.jpg")))
            .MustHaveHappened();
        // this shouldn't happen as matching key not found
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>._))
            .MustNotHaveHappened();
        
        // create sizes.json
        const string expected = "{\"o\":[[100,200],[50,100]],\"a\":[[512,1024],[200,400]]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureNewLayout_DeletesOldConfinedSquareLayout()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/low.jpg", 
                "2/1/the-astronaut/100.jpg", 
                "2/1/the-astronaut/sizes.json",
                "2/1/the-astronaut/full/50,100/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset {Width = 4000, Height = 8000, ThumbnailPolicy = "TheBestOne", ImageDeliveryChannels = new List<ImageDeliveryChannel>()
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicyId = 2,
                    DeliveryChannelPolicy = new DeliveryChannelPolicy()
                    {
                        Name = "default",
                        PolicyData = "[\"!200,200\",\"!100,100\"]",
                        Channel = AssetDeliveryChannels.Thumbnails
                    }
                }
            }});

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);
        var expectedDeletions = new[]
        {
            "the-bucket:::2/1/the-astronaut/100.jpg", "the-bucket:::2/1/the-astronaut/sizes.json"
        };

        A.CallTo(() => bucketWriter.DeleteFromBucket(A<ObjectInBucket[]>.That.Matches(a =>
            expectedDeletions.Contains(a[0].ToString()) && expectedDeletions.Contains(a[1].ToString())
        ))).MustHaveHappened();
    }
    
    [Fact]
    public async Task EnsureNewLayout_DoesNotMakeConcurrentAttempts_ForSameKey()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        var fakeBucketContents = new List<string> {"2/1/the-astronaut/200.jpg"};

        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .ReturnsLazily(() => fakeBucketContents.ToArray());

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset {Width = 200, Height = 250, ImageDeliveryChannels = imageDeliveryChannels});

        // Once called, add s.json to return list of bucket contents
        A.CallTo(() => bucketWriter.WriteToBucket(A<ObjectInBucket>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes(() => fakeBucketContents.Add("2/1/the-astronaut/s.json"));

        A.CallTo(() => bucketWriter.CopyObject(A<ObjectInBucket>._, A<ObjectInBucket>._))
            .Invokes(async () => await Task.Delay(500));

        var ensure1 = Task.Factory.StartNew(() => sut.EnsureNewLayout(assetId));
        var ensure2 = Task.Factory.StartNew(() => sut.EnsureNewLayout(assetId));

        // Act
        await Task.WhenAll(ensure1, ensure2);
        
        // Assert
        A.CallTo(() => assetRepository.GetAsset(assetId)).MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task EnsureNewLayout_AllowsConcurrentAttempts_ForDifferentKey()
    {
        var assetId1 = new AssetId(2, 1, "the-astronaut");
        var assetId2 = new AssetId(3, 1, "the-astronaut");

        var fakeBucketContents = new List<string> {"2/1/the-astronaut/200.jpg"};

        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId1.ToString()))))
            .ReturnsLazily(() =>  fakeBucketContents.ToArray());

        // Once called, add sizes.json to return list of bucket contents
        A.CallTo(() => bucketWriter.WriteToBucket(A<ObjectInBucket>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((ObjectInBucket dest, string _, string _) =>
                fakeBucketContents.Add(dest.Key + "sizes.json"));

        A.CallTo(() => bucketWriter.CopyObject(A<ObjectInBucket>._, A<ObjectInBucket>._))
            .Invokes(async () => await Task.Delay(500));

        var ensure1 = Task.Factory.StartNew(() => sut.EnsureNewLayout(assetId1));
        var ensure2 = Task.Factory.StartNew(() => sut.EnsureNewLayout(assetId2));

        // Act
        await Task.WhenAll(ensure1, ensure2);
        
        // Assert
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._))
            .MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task EnsureNewLayout_AssetNotFound()
    {
        // Arrange
        var assetId = new AssetId(2, 1, "doesnotexit");

        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
       
        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        A.CallTo(() => assetRepository.GetAsset(assetId)).MustHaveHappened();
        response.Should().Be(ReorganiseResult.AssetNotFound);
    }
    
    [Fact]
    public async Task EnsureNewLayout_HandlesDuplicateMaxSize()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/215,/0/default.jpg",
                "2/1/the-astronaut/full/215,400/0/default.jpg",
                "2/1/the-astronaut/full/216,/0/default.jpg",
                "2/1/the-astronaut/full/216,400/0/default.jpg",
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset 
            {
                Width = 1293, 
                Height = 2400, 
                MaxUnauthorised = -1, 
                ImageDeliveryChannels = new List<ImageDeliveryChannel>()
                {
                    new()
                    {
                        Channel = AssetDeliveryChannels.Thumbnails,
                        DeliveryChannelPolicyId = 2,
                        DeliveryChannelPolicy = new DeliveryChannelPolicy()
                        {
                            Name = "default",
                            PolicyData = "[\"!1024,1024\",\"!400,400\"]",
                            Channel = AssetDeliveryChannels.Thumbnails
                        }
                    }
                }
            });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);
        
        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/1024.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/216,400/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/400.jpg")))
            .MustHaveHappened(1, Times.Exactly);

        // create sizes.json
        const string expected = "{\"o\":[[552,1024],[216,400]],\"a\":[]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task EnsureNewLayout_CreatesExpectedResources_MixedAuthAndOpen_ImageSmallerThanThumbnail()
    {
        var assetId = new AssetId(2, 1, "the-astronaut");
        A.CallTo(() => bucketReader.GetMatchingKeys(
                A<ObjectInBucket>.That.Matches(o => o.Key.StartsWith(assetId.ToString()))))
            .Returns(new[]
            {
                "2/1/the-astronaut/full/200,/0/default.jpg",
                "2/1/the-astronaut/full/200,400/0/default.jpg",
                "2/1/the-astronaut/full/100,/0/default.jpg",
                "2/1/the-astronaut/full/100,200/0/default.jpg",
                "2/1/the-astronaut/full/50,/0/default.jpg",
                "2/1/the-astronaut/full/50,100/0/default.jpg"
            });

        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset
                { 
                    Width = 300, 
                    Height = 600, 
                    MaxUnauthorised = 350, 
                    Roles = "admin", 
                    ImageDeliveryChannels = imageDeliveryChannels
                });

        // Act
        var response = await sut.EnsureNewLayout(assetId);

        // Assert
        response.Should().Be(ReorganiseResult.Reorganised);

        // move jpg per thumbnail size
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/low.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/600.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/200,400/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/auth/400.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/100,200/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/200.jpg")))
            .MustHaveHappened();
        A.CallTo(() =>
                bucketWriter.CopyObject(
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/full/50,100/0/default.jpg"),
                    A<ObjectInBucket>.That.Matches(o => o.Key == "2/1/the-astronaut/open/100.jpg")))
            .MustHaveHappened();

        // create sizes.json
        const string expected = "{\"o\":[[100,200],[50,100]],\"a\":[[300,600],[200,400]]}";
        A.CallTo(() =>
                bucketWriter.WriteToBucket(
                    A<ObjectInBucket>.That.Matches(o =>
                        o.Bucket == "the-bucket" && o.Key == "2/1/the-astronaut/s.json"), expected,
                    "application/json", A<CancellationToken>._))
            .MustHaveHappened();
    }
}