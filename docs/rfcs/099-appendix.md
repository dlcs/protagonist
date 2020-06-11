But this is still our extra plumbing, more complexity to manage. 





One ideal scenario is an imaginary AWS offering - S3-backed volumes where you can specify a source bucket, and the size of "real" volume you want (e.g., 1TB). The file system view can be read only - we don't need to write to the bucket via a filesystem, we can do that as S3. Reads of the filesystem manage the orchestration at that file access, below our application logic. We just assume that if it's in the bucket, it can be read from the file system, and everything looks simple.

This is _like_ [S3fs-fuse](https://github.com/s3fs-fuse/s3fs-fuse), but a managed service.

Other people use S3fs-fuse with IIPImage, but either with customisations - https://github.com/klokantech/embedr/issues/16 - or interventions for cache management that are similar to what we're already doing, so not particularly easier to manage.

Other approaches we looked at 5 years ago were commercial offerings on top of AWS, like SoftNAS, but they didn't have quite the features we wanted.

There is an echo of AWS EFS in this. We tried using EFS rather than EBS for IIPImage, but found it too slow. A write operation is not finished until everything is consistent, and this was just too slow for image orchestration.

## Alternatives to Orchestration where possible

We're already offering the separate [`/thumbs/`](001-thumbnails.md) path for cases where the client knows what sizes to ask for.

We can take this further, see [Special Server](010-special-server.md) for further details.

<!-- what was here moved to 010-special-server.md -->

## Other things to look at

<!-- appendix -->

### AWS File Gateway

On the face of it, AWS Storage Gateway looks a lot like the hypothetical service described earlier: https://aws.amazon.com/storagegateway/file/

The File Gateway can be run on EC2.

However, there are some issues that would limit us:

> An object that needs to be accessed by using a file share should only be managed by the gateway. If you directly overwrite or update an object previously written by file gateway, it results in undefined behavior when the object is accessed through the file share.

This would preclude use cases where the DLCS makes use of the existing buckets of an [archival storage system](https://github.com/wellcomecollection/docs/blob/extract-docs/rfcs/002-archival_storage/README.md); we'd need to copy images into another S3 bucket, which means synchronisation issues as well as huge amounts of extra storage.
 
This could be an option for some scenarios though, and we could do some performance testing on it.

### Azure Data Lake Storage

https://docs.microsoft.com/en-gb/azure/storage/blobs/data-lake-storage-introduction

> Azure Data Lake Storage Gen2 is a set of capabilities dedicated to big data analytics, built on Azure Blob storage. Data Lake Storage Gen2 is the result of converging the capabilities of our two existing storage services, Azure Blob storage and Azure Data Lake Storage Gen1. Features from Azure Data Lake Storage Gen1, such as file system semantics, directory, and file level security and scale are combined with low-cost, tiered storage, high availability/disaster recovery capabilities from Azure Blob storage.

## Next steps

What are we missing here? What other ways of doing this are there? Is the system we've got actually the best way of doing it (with some modifications)?

Sources of concern:

A flood of tile requests for the same image can't all trigger orchestration of that image. We make it the equivalent of a critical section, we use a semaphore. While this is as light as possible, it still seems wasteful. Or at least, I'd rather it was someone else's problem.

How well do the mentioned solutions handle multiple concurrent demands for the same file?

What's the most efficient way to optimise this? Avoiding multiple orchestration attempts, but recognising that all the request are independent? We use Redis and some Lua code in NGINX. 
