using System.Net;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.MediaConvert;
using DLCS.AWS.MediaConvert.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Core.Caching;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
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
            new MediaConvertResponseConverter(NullLogger<MediaConvertResponseConverter>.Instance),
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
}
