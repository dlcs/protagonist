using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Strings;
using FluentAssertions.Execution;

namespace Test.Helpers.Storage;

/// <summary>
/// Test bucket writer implementation that maintains in-memory list of addition/copy.
/// </summary>
public class TestBucketWriter : IBucketWriter
{
    public Dictionary<string, BucketObject> Operations { get; } = new();
    private readonly string? forBucket;
    private readonly List<string> verifiedPaths = new();
    
    public TestBucketWriter(string? bucket = null)
    {
        forBucket = bucket;
    }
    
    /// <summary>
    /// Assert key exists.
    /// </summary>
    public BucketObject ShouldHaveKey(string key)
    {
        if (Operations.TryGetValue(key, out var op))
        {
            verifiedPaths.Add(key);
            return op;
        }

        throw new AssertionFailedException($"{key} not found");
    }
        
    /// <summary>
    /// Assert key exists.
    /// </summary>
    public TestBucketWriter ShouldNotHaveKey(string key)
    {
        if (Operations.TryGetValue(key, out var op))
        {
            throw new AssertionFailedException($"{key} found but should not exist");
        }

        return this;
    }

    /// <summary>
    /// Assert all keys have been verified.
    /// </summary>
    public void ShouldHaveNoUnverifiedPaths()
    {
        var unverified = Operations.Select(kvp => kvp.Key).Except(verifiedPaths);
        if (unverified.Any())
        {
            throw new AssertionFailedException($"The following paths have not been verified: {string.Join(",", unverified)}");
        }
    }

    public Task CopyObject(ObjectInBucket source, ObjectInBucket destination)
    {
        Operations[destination.Key] = new BucketObject { Bucket = destination.Bucket };
        return Task.CompletedTask;
    }

    public async Task<LargeObjectCopyResult> CopyLargeObject(ObjectInBucket source, ObjectInBucket destination,
        Func<long, Task<bool>> verifySize = null, bool destIsPublic = false, CancellationToken token = default)
    {
        Operations[destination.Key] = new BucketObject { Bucket = destination.Bucket };

        const long size = 100;
        if (verifySize != null)
        {
            if (!await verifySize(size))
            {
                return new LargeObjectCopyResult(LargeObjectStatus.FileTooLarge, size);        
            }
        }

        return new LargeObjectCopyResult(LargeObjectStatus.Success, size);
    }

    public Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
        CancellationToken cancellationToken = default)
    {
        if (forBucket.HasText() && dest.Bucket != forBucket)
            throw new InvalidOperationException("Operation for different bucket");

        Operations[dest.Key] = new BucketObject { Contents = content, Bucket = dest.Bucket };
        return Task.FromResult(true);
    }

    public Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string contentType = null)
    {
        if (forBucket.HasText() && dest.Bucket != forBucket)
            throw new InvalidOperationException("Operation for different bucket");

        Operations[dest.Key] = new BucketObject { ContentStream = content, Bucket = dest.Bucket };
        return Task.FromResult(true);
    }

    public Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string contentType = null,
        CancellationToken token = default)
    {
        if (forBucket.HasText() && dest.Bucket != forBucket)
            throw new InvalidOperationException("Operation for different bucket");

        Operations[dest.Key] = new BucketObject { FilePath = filePath, Bucket = dest.Bucket };
        return Task.FromResult(true);
    }

    public Task DeleteFromBucket(params ObjectInBucket[] toDelete)
    {
        foreach (ObjectInBucket o in toDelete)
        {
            if (forBucket.HasText() && o.Bucket != forBucket)
                throw new InvalidOperationException("Operation for different bucket");

            if (Operations.ContainsKey(o.Key))
            {
                Operations.Remove(o.Key);
            }
        }

        return Task.CompletedTask;
    }
}

public class BucketObject
{
    public string FilePath { get; set; }
    public string Contents { get; set; }
    
    public string Bucket { get; set; }

    public Stream ContentStream { get; set; }

    /// <summary>
    /// Assert object has expected file path.
    /// </summary>
    public BucketObject WithFilePath(string filePath)
    {
        if (FilePath != filePath)
        {
            throw new AssertionFailedException($"FilePath expected {filePath} but was {FilePath}");
        }

        return this;
    }

    /// <summary>
    /// Assert object has expected contents.
    /// </summary>
    public BucketObject WithContents(string contents)
    {
        if (Contents != contents)
        {
            throw new AssertionFailedException($"FilePath expected {contents} but was {Contents}");
        }

        return this;
    }
    
    /// <summary>
    /// Assert object is in expected bucket
    /// </summary>
    public BucketObject ForBucket(string bucket)
    {
        if (Bucket != bucket)
        {
            throw new AssertionFailedException($"bucket expected {bucket} but was {Bucket}");
        }

        return this;
    }
}