using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using FluentAssertions.Execution;

namespace Test.Helpers.Storage;

/// <summary>
/// Test bucket writer implementation that maintains in-memory list of addition/copy.
/// </summary>
public class TestBucketWriter : IBucketWriter
{
    public Dictionary<string, BucketObject> Operations { get; } = new();
    private readonly string forBucket;
    private readonly List<string> verifiedPaths = new();
    
    public TestBucketWriter(string bucket)
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
    
    public Task CopyWithinBucket(string bucket, string sourceKey, string destKey)
    {
        if (bucket != forBucket) throw new InvalidOperationException("Operation for different bucket");
            
        if (Operations.TryGetValue(sourceKey, out var op))
        {
            Operations[destKey] = new BucketObject {Contents = op.Contents, FilePath = op.FilePath};
            return Task.FromResult(true);
        }
            
        return Task.FromResult(false);
    }

    public Task CopyObject(ObjectInBucket source, ObjectInBucket destination)
    {
        throw new System.NotImplementedException();
    }

    public Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
        CancellationToken cancellationToken = default)
    {
        if (dest.Bucket != forBucket) throw new InvalidOperationException("Operation for different bucket");

        Operations[dest.Key] = new BucketObject {Contents = content};
        return Task.FromResult(true);
    }

    public Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string contentType = null)
    {
        if (dest.Bucket != forBucket) throw new InvalidOperationException("Operation for different bucket");

        Operations[dest.Key] = new BucketObject {ContentStream = content};
        return Task.FromResult(true);
    }

    public Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string contentType = null)
    {
        if (dest.Bucket != forBucket) throw new InvalidOperationException("Operation for different bucket");

        Operations[dest.Key] = new BucketObject {FilePath = filePath};
        return Task.FromResult(true);
    }

    public Task DeleteFromBucket(params ObjectInBucket[] toDelete)
    {
        foreach (ObjectInBucket o in toDelete)
        {
            if (o.Bucket != forBucket) throw new InvalidOperationException("Operation for different bucket");

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
}