# 'PDF as input' storage

This follows on from [RFC 011 - PDF as Input](011-pdfs-as-input.md), which has been implemented as the [composite-handler](https://github.com/dlcs/composite-handler/).

As detailed in the previous RFC, after rasterizing each page of the PDF the individual images are uploaded to DLCS-managed storage location, the `OriginBucket`. This RFC proposes changing how those items are stored to avoid storing multiple redundant copies of rasterized jpegs.

## Problem

We need to handle superseded composite requests better. The composite-handler currently saves the rasterized images to a folder in S3 prefixed with a guid, this location is used as the origin for images in DLCS. If a PDF is reingested it will use a new guid and those files will accrue over time. 

Depending on the rate of change in PDFs this may not be an issue but over time could lead to storing extra GB of images that are then difficult to attribute back to specific customers/spaces/image/batch request.

Detailed below is current storage key format and proposed change.

## Current Format

The current format for upload locations is:

* `s3://{OriginBucket}/composites/{SubmissionId}/{Filename}`
* e.g. `s3://dlcs-test-storage-origin/composites/0080d2c9-9110-4211-b910-df740fdbb8f9/image-0003-21.jpg`

The main issue with the above is that `SubmissionId` is an internal identifier that is relevant to composite-handler only, it is a transient identifier relevant for the processing of a single composite-handler request. The result is that an identical payload to ingest a 500 page PDF is submitted 3 times there would be 500 images in the DLCS but 1500 jpegs stored in S3, with 1000 of those orphaned. Overtime this could result in a lot of redundant storage that will be difficult to cleanup without checking whether specific keys are still used as origins in the DLCS.

The second issue is that there is no `{CustomerId}` or `{SpaceId}` in the key so it is impossible to tell which customer each submission belongs to, without looking it up in the composite-handler database.

## Proposed New Format

I suggest we use the following new format for storing rasterized images:

* `s3://{OriginBucket}/composites/{CustomerId}/{SpaceId}/{CompositeId}/{Filename}`
* e.g. `s3://dlcs-test-storage-origin/composites/2/10/68401/image-0003-21.jpg` or `s3://dlcs-test-storage-origin/composites/2/10/0080d2c9-9110-4211-b910-df740fdbb8f9/image-0003-21.jpg`

Where `CompositeId` is a new, optional, extra property that can be submitted to the composite-handler as part of a Composite payload. This value is any string that the calling system uses to uniquely identify the PDF. The composite-handler doesn't assign any meaning to this, it's only used to specify the storage location in S3. It will fallback to current `SubmissionId` value if not provided.

`CustomerId` and `SpaceId` are already stored by composite-handler and are included in the path for a couple of reasons:
* It is consistent with other DLCS keys.
* Allows easy cleanup if a customer or space is deleted. 
* Avoids collisions with other caller specified `CompositeId` values.

A sample payload using `CompositeId` could be:

```json
{
  "@context": "http://www.w3.org/ns/hydra/context.jsonld",
  "@type": "Collection",
  "member": [
    {
      "@type": "vocab:Composite",
      "id": "my-pdf-{:03d}",
      "space": 6,
      "origin": "https://my-test-host.example/my-pdf.pdf",
      "string1": "my-id-{:03d}",
      "string2": "other_id",
      "number1": "0",
      "number2": "{:03d}",
      "roles": [],
      "maxUnauthorised": -1,
      "incrementSeed": 0,
      "compositeId": "my-internal-identifier-123"
    }
  ]
}
```

### Side effects

If a PDF is resubmitted and has decreased in size, say from 200 -> 150 pages, there could still be orphaned images stored in S3. This number is likely to be negligable unless there are huge variations in size. A separate process could still identify orphans and delete them, e.g., an orphan check lambda runs after a PDF is processed.

If a PDF has been reordered and an image is reingested directly via the DLCS (i.e. a direct `/reingest` call) between resubmitted PDF rasterization and DLCS batch completion then there could be a transient inconsistency in how that series of PDF images would look (e.g. when output via a NQ). This is highly unlikely and would shortly be consistent once the batch has successfully processed.