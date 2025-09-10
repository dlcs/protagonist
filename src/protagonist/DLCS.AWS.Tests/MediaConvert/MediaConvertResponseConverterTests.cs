using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.MediaConvert;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core.Types;

namespace DLCS.AWS.Tests.MediaConvert;

// These tests aren't exhaustive, test general processing due to onerous setup
public class MediaConvertResponseConverterTests
{
    [Fact]
    public void CreateTranscoderJob_ReturnsExpected_SingleAudio()
    {
        var assetId = new AssetId(1, 2, "foo");
        var singleAudio = new Job
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
                            DurationInMs = 12340,
                        }
                    ]
                }
            ],
            Settings = new JobSettings
            {
                Inputs =
                [
                    new Input { FileInput = "s3://input/file.wav" }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            FileGroupSettings = new FileGroupSettings
                                { Destination = "s3://output/1/2/foo/transcode" }
                        },
                        Outputs =
                        [
                            new Output
                            {
                                Extension = "mp3",
                                Preset = "128k_mp3-preset",
                                NameModifier = "_1",
                            }
                        ]
                    },
                ]
            },
            Timing = new Timing
            {
                FinishTime = new DateTime(2025, 9, 9, 10, 0, 0),
                StartTime = new DateTime(2025, 9, 9, 9, 0, 0),
                SubmitTime = new DateTime(2025, 9, 9, 8, 0, 0),
            },
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        };

        var expected = new TranscoderJob
        {
            Id = "fake-for-test",
            CreatedAt = singleAudio.CreatedAt,
            Status = "COMPLETE",
            PipelineId = "the-queue",
            Timing = new TranscoderJob.TranscoderTiming
            {
                FinishTimeMillis = 1757408400000,
                StartTimeMillis = 1757404800000,
                SubmitTimeMillis = 1757401200000,
            },
            Input = new TranscoderJob.TranscoderInput { Input = "s3://input/file.wav" },
            Outputs =
            [
                new TranscoderJob.TranscoderOutput
                {
                    Id = "0",
                    Duration = 12,
                    DurationMillis = 12340,
                    TranscodeKey = "1/2/foo/transcode_1.mp3",
                    Key = "1/2/foo/full/max/default.mp3",
                    Extension = "mp3",
                    PresetId = "128k_mp3-preset",
                }
            ],
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        };
        
        var result = MediaConvertResponseConverter.CreateTranscoderJob(singleAudio, assetId);
        
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void CreateTranscoderJob_ReturnsExpected_MultiVideo()
    {
        var assetId = new AssetId(1, 2, "foo");
        var singleAudio = new Job
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
                            DurationInMs = 12340,
                            VideoDetails = new VideoDetail
                            {
                                HeightInPx = 720,
                                WidthInPx = 1280,
                            }
                        },
                        new OutputDetail
                        {
                            DurationInMs = 12344,
                            VideoDetails = new VideoDetail
                            {
                                HeightInPx = 1080,
                                WidthInPx = 1920,
                            }
                        }
                    ]
                }
            ],
            Settings = new JobSettings
            {
                Inputs =
                [
                    new Input { FileInput = "s3://input/file.raw" }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            FileGroupSettings = new FileGroupSettings
                                { Destination = "s3://output/1/2/foo/transcode" }
                        },
                        Outputs =
                        [
                            new Output
                            {
                                Extension = "mp4",
                                Preset = "mp4-hd",
                                NameModifier = "_1",
                            },
                            new Output
                            {
                                Extension = "mkv",
                                Preset = "1080-hd-mkv",
                                NameModifier = "_2",
                            }
                        ]
                    },
                ]
            },
            Timing = new Timing
            {
                FinishTime = new DateTime(2025, 9, 9, 10, 0, 0),
                StartTime = new DateTime(2025, 9, 9, 9, 0, 0),
                SubmitTime = new DateTime(2025, 9, 9, 8, 0, 0),
            },
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "video/raw",
                ["dlcsId"] = "1/2/foo"
            }
        };

        var expected = new TranscoderJob
        {
            Id = "fake-for-test",
            CreatedAt = singleAudio.CreatedAt,
            Status = "COMPLETE",
            PipelineId = "the-queue",
            Timing = new TranscoderJob.TranscoderTiming
            {
                FinishTimeMillis = 1757408400000,
                StartTimeMillis = 1757404800000,
                SubmitTimeMillis = 1757401200000,
            },
            Input = new TranscoderJob.TranscoderInput { Input = "s3://input/file.raw" },
            Outputs =
            [
                new TranscoderJob.TranscoderOutput
                {
                    Id = "0",
                    Duration = 12,
                    DurationMillis = 12340,
                    Width = 1280,
                    Height = 720,
                    TranscodeKey = "1/2/foo/transcode_1.mp4",
                    Key = "1/2/foo/full/full/max/max/0/default.mp4",
                    Extension = "mp4",
                    PresetId = "mp4-hd",
                },
                new TranscoderJob.TranscoderOutput
                {
                    Id = "1",
                    Duration = 12,
                    DurationMillis = 12344,
                    Width = 1920,
                    Height = 1080,
                    TranscodeKey = "1/2/foo/transcode_2.mkv",
                    Key = "1/2/foo/full/full/max/max/0/default.mkv",
                    Extension = "mkv",
                    PresetId = "1080-hd-mkv",
                }
            ],
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "video/raw",
                ["dlcsId"] = "1/2/foo"
            }
        };
        
        var result = MediaConvertResponseConverter.CreateTranscoderJob(singleAudio, assetId);
        
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void CreateTranscoderJob_SetsErrorCodes_HandlesEmptyOutputGroupDetails_IfError()
    {
        var assetId = new AssetId(1, 2, "foo");
        var singleAudio = new Job
        {
            Id = "fake-for-test",
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.ERROR,
            ErrorCode = 1040,
            ErrorMessage = "Duplicate output paths [s3://path] found in input job.",
            Queue = "arn:aws:mediaconvert:eu-west-1:123456789012:queues/the-queue",
            Settings = new JobSettings
            {
                Inputs =
                [
                    new Input { FileInput = "s3://input/file.wav" }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            FileGroupSettings = new FileGroupSettings
                                { Destination = "s3://output/1/2/foo/transcode" }
                        },
                        Outputs =
                        [
                            new Output
                            {
                                Extension = "mp3",
                                Preset = "128k_mp3-preset",
                                NameModifier = "_1",
                            }
                        ]
                    },
                ]
            },
            Timing = new Timing
            {
                FinishTime = new DateTime(2025, 9, 9, 10, 0, 0),
                StartTime = new DateTime(2025, 9, 9, 9, 0, 0),
                SubmitTime = new DateTime(2025, 9, 9, 8, 0, 0),
            },
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        };

        var expected = new TranscoderJob
        {
            Id = "fake-for-test",
            CreatedAt = singleAudio.CreatedAt,
            Status = "ERROR",
            PipelineId = "the-queue",
            Timing = new TranscoderJob.TranscoderTiming
            {
                FinishTimeMillis = 1757408400000,
                StartTimeMillis = 1757404800000,
                SubmitTimeMillis = 1757401200000,
            },
            Input = new TranscoderJob.TranscoderInput { Input = "s3://input/file.wav" },
            Outputs = [],
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            },
            ErrorCode = 1040,
            ErrorMessage = "Duplicate output paths [s3://path] found in input job."
        };
        
        var result = MediaConvertResponseConverter.CreateTranscoderJob(singleAudio, assetId);
        
        result.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData("CANCELED")]
    [InlineData("PROGRESSING")]
    [InlineData("SUBMITTED")]
    public void CreateTranscoderJob_DoesNotSetOutputKey_IfNotComplete(string status)
    {
        var assetId = new AssetId(1, 2, "foo");
        var singleAudio = new Job
        {
            Id = "fake-for-test",
            CreatedAt = DateTime.UtcNow,
            Status = status,
            Queue = "arn:aws:mediaconvert:eu-west-1:123456789012:queues/the-queue",
            OutputGroupDetails =
            [
                new OutputGroupDetail
                {
                    OutputDetails =
                    [
                        new OutputDetail
                        {
                            DurationInMs = 12340,
                        }
                    ]
                }
            ],
            Settings = new JobSettings
            {
                Inputs =
                [
                    new Input { FileInput = "s3://input/file.wav" }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            FileGroupSettings = new FileGroupSettings
                                { Destination = "s3://output/1/2/foo/transcode" }
                        },
                        Outputs =
                        [
                            new Output
                            {
                                Extension = "mp3",
                                Preset = "128k_mp3-preset",
                                NameModifier = "_1",
                            }
                        ]
                    },
                ]
            },
            Timing = new Timing
            {
                FinishTime = new DateTime(2025, 9, 9, 10, 0, 0),
                StartTime = new DateTime(2025, 9, 9, 9, 0, 0),
                SubmitTime = new DateTime(2025, 9, 9, 8, 0, 0),
            },
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        };

        var expected = new TranscoderJob
        {
            Id = "fake-for-test",
            CreatedAt = singleAudio.CreatedAt,
            Status = status,
            PipelineId = "the-queue",
            Timing = new TranscoderJob.TranscoderTiming
            {
                FinishTimeMillis = 1757408400000,
                StartTimeMillis = 1757404800000,
                SubmitTimeMillis = 1757401200000,
            },
            Input = new TranscoderJob.TranscoderInput { Input = "s3://input/file.wav" },
            Outputs =
            [
                new TranscoderJob.TranscoderOutput
                {
                    Id = "0",
                    Duration = 12,
                    DurationMillis = 12340,
                    TranscodeKey = "1/2/foo/transcode_1.mp3",
                    Extension = "mp3",
                    PresetId = "128k_mp3-preset",
                }
            ],
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        };
        
        var result = MediaConvertResponseConverter.CreateTranscoderJob(singleAudio, assetId);
        
        result.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public void CreateTranscoderJob_HandlesMinDateTimeTimings()
    {
        // MC model Timings are DateTime properties backed by DateTime?, the getters for these call .GetValueOrDefault()
        // in the event if the backing store being null (e.g. if job hasn't finished) we'll get DateTime.Min, which
        // would result in a negative timestamp
        var assetId = new AssetId(1, 2, "foo");
        var singleAudio = new Job
        {
            Id = "fake-for-test",
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.ERROR,
            Queue = "arn:aws:mediaconvert:eu-west-1:123456789012:queues/the-queue",
            Settings = new JobSettings
            {
                Inputs =
                [
                    new Input { FileInput = "s3://input/file.wav" }
                ],
                OutputGroups =
                [
                    new OutputGroup
                    {
                        OutputGroupSettings = new OutputGroupSettings
                        {
                            FileGroupSettings = new FileGroupSettings
                                { Destination = "s3://output/1/2/foo/transcode" }
                        },
                        Outputs =
                        [
                            new Output
                            {
                                Extension = "mp3",
                                Preset = "128k_mp3-preset",
                                NameModifier = "_1",
                            }
                        ]
                    },
                ]
            },
            Timing = new Timing
            {
                FinishTime = DateTime.MinValue,
                StartTime = DateTime.MinValue,
                SubmitTime = new DateTime(2025, 9, 9, 8, 0, 0),
            },
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            }
        };

        var expected = new TranscoderJob
        {
            Id = "fake-for-test",
            CreatedAt = singleAudio.CreatedAt,
            Status = "ERROR",
            PipelineId = "the-queue",
            Timing = new TranscoderJob.TranscoderTiming
            {
                FinishTimeMillis = null,
                StartTimeMillis = null,
                SubmitTimeMillis = 1757401200000,
            },
            Input = new TranscoderJob.TranscoderInput { Input = "s3://input/file.wav" },
            Outputs = [],
            UserMetadata = new Dictionary<string, string>
            {
                ["mediaType"] = "audio/mp3",
                ["dlcsId"] = "1/2/foo"
            },
        };
        
        var result = MediaConvertResponseConverter.CreateTranscoderJob(singleAudio, assetId);
        
        result.Should().BeEquivalentTo(expected);
    }
}
