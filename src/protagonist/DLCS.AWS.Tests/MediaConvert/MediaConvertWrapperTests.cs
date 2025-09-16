using System.Net;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.MediaConvert;
using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;
using Test.Helpers.Data;
using Test.Helpers.Settings;
using Test.Helpers.Storage;

namespace DLCS.AWS.Tests.MediaConvert;

public class MediaConvertWrapperTests
{
    private readonly IAmazonMediaConvert mediaConvert;
    private readonly MediaConvertWrapper sut;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly TestBucketWriter bucketWriter;
    private readonly IBucketReader bucketReader;
    
    public MediaConvertWrapperTests()
    {
        mediaConvert = A.Fake<IAmazonMediaConvert>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = new TestBucketWriter();
        bucketReader = A.Fake<IBucketReader>();

        var awsSettings = new AWSSettings
        {
            Transcode =
            {
                RoleArn = "arn:12345"
            }
        };

        sut = new MediaConvertWrapper(mediaConvert, new MockCachingService(), bucketWriter, bucketReader,
            storageKeyGenerator, OptionsHelpers.GetOptionsMonitor(new CacheSettings()),
            OptionsHelpers.GetOptionsMonitor(awsSettings),
            NullLogger<MediaConvertWrapper>.Instance);
    }
    
    [Fact]
    public async Task GetPipelineId_ReturnsNull_IfPipelineNotFound()
    {
        A.CallTo(() => mediaConvert.GetQueueAsync(A<GetQueueRequest>._, A<CancellationToken>._))
            .Returns(new GetQueueResponse { Queue = new Queue(), HttpStatusCode = HttpStatusCode.BadGateway });

        var result = await sut.GetPipelineId("hi");

        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetPipelineId_ReturnsArn_IfPipelineFound()
    {
        const string queueArn = "arn:1234:queue";
        A.CallTo(() =>
                mediaConvert.GetQueueAsync(A<GetQueueRequest>.That.Matches(r => r.Name == "hi"),
                    A<CancellationToken>._))
            .Returns(new GetQueueResponse
                { Queue = new Queue { Arn = queueArn }, HttpStatusCode = HttpStatusCode.OK });

        var result = await sut.GetPipelineId("hi");

        result.Should().Be(queueArn);
    }

    [Fact]
    public async Task CreateJob_CreatesJobWithExpectedInput()
    {
        var job = new MediaConvertJobGroup(new ObjectInBucket("output", "dest"), new[]
        {
            new MediaConvertOutput("Preset-ABC123", "mp4", "_16:9"),
            new MediaConvertOutput("Preset-DEF456", "avi")
        });
        var metadata = new Dictionary<string, string>
        {
            ["foo"] = "bar"
        };
        const string inputKey = "s3://bucket/key";
        const string queueId = "every-country's-sun";

        List<OutputGroup> expectedOutputGroup =
        [
            new()
            {
                OutputGroupSettings = new OutputGroupSettings
                {
                    FileGroupSettings = new FileGroupSettings
                    {
                        Destination = "s3://output/dest"
                    },
                    Type = OutputGroupType.FILE_GROUP_SETTINGS,
                },
                Outputs =
                [
                    new Output
                    {
                        Preset = "Preset-ABC123",
                        Extension = "mp4",
                        NameModifier = "_16:9"
                    },
                    new Output
                    {
                        Preset = "Preset-DEF456",
                        Extension = "avi",
                        NameModifier = "_1" // No modifier set so defaults to index
                    }
                ]
            }
        ];

        CreateJobRequest createRequest = null!;
        A.CallTo(() => mediaConvert.CreateJobAsync(A<CreateJobRequest>._, A<CancellationToken>._))
            .Invokes((CreateJobRequest request, CancellationToken _) =>
            {
                createRequest = request;
            })
            .Returns(new CreateJobResponse { Job = new Job() });

        // Act
        await sut.CreateJob(inputKey, queueId, job, metadata, CancellationToken.None);
        
        // Assert
        createRequest.UserMetadata.Should().Equal(metadata, "Metadata passed untouched");
        createRequest.Queue.Should().Be(queueId);
        createRequest.Role.Should().Be("arn:12345", "ARN read from config");
        createRequest.Settings.OutputGroups.Should().BeEquivalentTo(expectedOutputGroup);
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task CreateJob_ReturnsCorrectResponse(HttpStatusCode statusCode)
    {
        // Arrange
        var job = new MediaConvertJobGroup(new ObjectInBucket("output", "dest"), new[]
        {
            new MediaConvertOutput("Preset-ABC123", "mp4", "_16:9"),
            new MediaConvertOutput("Preset-DEF456", "avi")
        });
        
        A.CallTo(() => mediaConvert.CreateJobAsync(A<CreateJobRequest>._, A<CancellationToken>._))
            .Returns(new CreateJobResponse { Job = new Job { Id = "job-1234" }, HttpStatusCode = statusCode });

        // Act
        var response = await sut.CreateJob("inputKey", "queueId", job, new Dictionary<string, string>(),
            CancellationToken.None);
        
        // Assert
        response.JobId.Should().Be("job-1234");
        response.HttpStatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task PersistJobId_WritesDataWithContentType()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var objectInBucket = new ObjectInBucket("output", "mocked-dest");
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(assetId))
            .Returns(objectInBucket);

        const string expectedPayload = "{\"jobId\":\"job-123\",\"transcodingService\":\"MediaConvert\"}";
        
        // Act
        await sut.PersistJobId(assetId, "job-123", CancellationToken.None);
        
        // Assert
        bucketWriter
            .ShouldHaveKey("mocked-dest")
            .ForBucket("output")
            .WithContents(expectedPayload)
            .WithContentType("application/json");
    }

    [Fact]
    public async Task GetTranscoderJob_ForAssetOnly_Null_IfKeyNotFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var objectInBucket = new ObjectInBucket("storage", "mocked-metadata");
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(assetId))
            .Returns(objectInBucket);
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Stream.Null, null));
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, CancellationToken.None);
        
        // Assert
        result.Should().BeNull("No metadata found");
    }
    
    [Fact]
    public async Task GetTranscoderJob_ForAssetOnly_Null_IfKeyFoundAndXML()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var objectInBucket = new ObjectInBucket("storage", "mocked-metadata");
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(assetId))
            .Returns(objectInBucket);
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, Stream.Null, new ObjectInBucketHeaders
            {
                ContentType = "application/xml"
            }));
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, CancellationToken.None);
        
        // Assert
        result.Should().BeNull("Metadata found but XML so older format (ElasticTranscoder)");
    }
    
    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"transcodingService\": \"MediaConvert\", \"jobId\": \"\" }")]
    public async Task GetTranscoderJob_ForAssetOnly_Null_IfKeyFoundButInvalidJson(string storedJson)
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var objectInBucket = new ObjectInBucket("storage", "mocked-metadata");
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(assetId))
            .Returns(objectInBucket);
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, storedJson.ToMemoryStream(), new ObjectInBucketHeaders
            {
                ContentType = "application/json"
            }));
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, CancellationToken.None);
        
        // Assert
        result.Should().BeNull("Metadata found but unknown JSON");
    }
    
    [Fact]
    public async Task GetTranscoderJob_ForAssetOnly_ReturnsNull_IfJobNotFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var objectInBucket = new ObjectInBucket("storage", "mocked-metadata");
        const string jobId = "clown";
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(assetId))
            .Returns(objectInBucket);
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, $"{{\"jobId\":\"{jobId}\"}}".ToMemoryStream(), new ObjectInBucketHeaders
            {
                ContentType = "application/json"
            }));
        A.CallTo(() =>
                mediaConvert.GetJobAsync(A<GetJobRequest>.That.Matches(r => r.Id == jobId), A<CancellationToken>._))
            .Returns<GetJobResponse?>(null);
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, CancellationToken.None);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetTranscoderJob_ForAssetOnly_ReturnsTranscoderJob()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var objectInBucket = new ObjectInBucket("storage", "mocked-metadata");
        const string jobId = "clown";
        A.CallTo(() => storageKeyGenerator.GetTimebasedMetadataLocation(assetId))
            .Returns(objectInBucket);
        A.CallTo(() => bucketReader.GetObjectFromBucket(objectInBucket, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(objectInBucket, $"{{\"jobId\":\"{jobId}\"}}".ToMemoryStream(), new ObjectInBucketHeaders
            {
                ContentType = "application/json"
            }));
        A.CallTo(() =>
                mediaConvert.GetJobAsync(A<GetJobRequest>.That.Matches(r => r.Id == jobId), A<CancellationToken>._))
            .Returns(MinMediaConvertJob);
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, CancellationToken.None);
        
        // Assert
        result.Id.Should().Be("fake-for-test");
    }
    
    [Fact]
    public async Task GetTranscoderJob_ReturnsNull_IfJobNotFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        const string jobId = "clown";
        A.CallTo(() =>
                mediaConvert.GetJobAsync(A<GetJobRequest>.That.Matches(r => r.Id == jobId), A<CancellationToken>._))
            .Returns<GetJobResponse?>(null);
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, jobId, CancellationToken.None);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetTranscoderJob_ReturnsNull_IfTranscoderJobForDifferentAsset()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        const string jobId = "clown";
        A.CallTo(() =>
                mediaConvert.GetJobAsync(A<GetJobRequest>.That.Matches(r => r.Id == jobId), A<CancellationToken>._))
            .Returns(MinMediaConvertJob);
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, jobId, CancellationToken.None);
        
        // Assert
        result.Should().BeNull("AssetId in MediaConvert Job does not match the AssetId provided");
    }
    
    [Fact]
    public async Task GetTranscoderJob_ReturnsTranscoderJob_IfFound()
    {
        var assetId = new AssetId(1, 2, "foo"); // This matches the assetId in fake payload
        const string jobId = "clown";
        A.CallTo(() =>
                mediaConvert.GetJobAsync(A<GetJobRequest>.That.Matches(r => r.Id == jobId), A<CancellationToken>._))
            .Returns(MinMediaConvertJob);
        
        // Act
        var result = await sut.GetTranscoderJob(assetId, jobId, CancellationToken.None);
        
        // Assert
        result.Id.Should().Be("fake-for-test");
    }

    // This is a MediaConvert GetJob response that has all required props to avoid breaking conversion
    private static readonly GetJobResponse MinMediaConvertJob = new()
    {
        Job = new Job
        {
            Id = "fake-for-test",
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.COMPLETE,
            Queue = "arn:aws:mediaconvert:eu-west-1:123456789012:queues/the-queue",
            OutputGroupDetails =
            [
                new OutputGroupDetail
                {
                    OutputDetails =
                    [
                        new OutputDetail
                        {
                            DurationInMs = 1234,
                        }
                    ]
                }
            ],
            Settings = new JobSettings
            {
                Inputs =
                [
                    new Input { FileInput = "s3://input/file" }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings()
                        {
                            FileGroupSettings = new FileGroupSettings { Destination = "s3://somewhere/here" }
                        },
                        Outputs =
                        [
                            new Output
                            {
                                Extension = "mp3",
                                Preset = "preset",

                            }
                        ]
                    },
                ]
            },
            Timing = new Timing
            {
                FinishTime = DateTime.UtcNow,
                StartTime = DateTime.UtcNow,
                SubmitTime = DateTime.UtcNow
            },
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        }
    };
}
