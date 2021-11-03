# PDFs (and other documents) as input

Scenario: you have a single (but probably multi-page) PDF, rather than a set of image files. You would like to have a IIIF Manifest for this PDF, so it can be loaded into IIIF Viewers, annotated as IIIF Canvases, and so on.

One way of doing this would be to allow the DLCS to accept a PDF as input, and have it extract the images for the pages and provide them as independent image services.

## POST a PDF to the DLCS

At the moment, you POST a [hydra:Collection](https://www.hydra-cg.com/spec/latest/core/#collections) of Images to the [queue](https://dlcs-book.readthedocs.io/en/latest/API_Reference/queue.html), or PUT a single [Image](https://dlcs-book.readthedocs.io/en/latest/API_Reference/image.html) to its location.


Current example of a Collection POSTed to the DLCS queue (here just containing one Image):

```json
{
  "@context": "http://www.w3.org/ns/hydra/context.jsonld",
  "@type": "Collection",
  "member": [
    {
      "id": "b28802068_0008.jp2",
      "space": 6,
      "origin": "https://s3-eu-west-1.amazonaws.com/bucketname/key-path/b28802068_0008.jp2",
      "string1": "b28802068",
      "string2": "",
      "string3": "",
      "number1": 0,
      "number2": 8,
      "number3": 0,
      "roles": [],
      "duration": 0,
      "family": "I",
      "mediaType": "image/jp2",
      "text": "https://api.wellcomecollection.org/text/alto/b28802068/b28802068_0008.jp2",
      "textType": "alto",
      "maxUnauthorised": -1,
    }
  ]
}
```

The response body is an accepted [Batch](https://dlcs-book.readthedocs.io/en/latest/API_Reference/batch.html) - the batch's images haven't been processed yet, but they are in the queue and will be processed in time. This could be a long time; it depends entirely on the number of images in the queue and the resources available to the DLCS to process them. Crucially, it's a very lightweight operation to enqueue things, so bursts of activity at ingest don't overwhelm the DLCS, it is able to spread the more intensive work over as much time as is needed to do it.


```json
{
  "@context": "https://api.dlcs.io/contexts/Batch.jsonld",
  "@id": "https://api.dlcs.io/customers/2/queue/batches/761372",
  "@type": "vocab:Batch",
  "errorImages": "https://api.dlcs.io/customers/2/queue/batches/761372/errorImages",
  "images": "https://api.dlcs.io/customers/2/queue/batches/761372/images",
  "completedImages": "https://api.dlcs.io/customers/2/queue/batches/761372/completedImages",
  "test": "https://api.dlcs.io/customers/2/queue/batches/761372/test",
  "submitted": "2021-02-17T15:21:12.0443523+00:00",
  "count": 36,
  "completed": 0,
  "errors": 0,
  "finished": "0001-01-01T00:00:00",
  "superseded": false
}
```

The `images` property in the above links to a collection of the images in the batch. 

The new requirement is that a PDF becomes a set of individual image services.

A PDF here is a bit like a _latent_ Collection. A `hydra:Collection` can have any domain class as members. This suggests we can introduce a new class to the API of the DLCS, stepping back a bit from the specifics of PDF:


### Composite

| Composite       |
|-----------------|
| id              |
| space           |
| origin          |
| string1         |
| string2         |
| string3         |
| number1         |
| number2         |
| number3         |
| roles           |
| duration        |
| family          |
| mediaType       |
| text            |
| textType        |
| maxUnauthorised |
| originFormat    |
| incrementSeed   |

Submitting a `Composite` to the queue is telling the DLCS "unpack the sequence of images inside the resource, and create a DLCS image for each one, assigning the properties provided to each DLCS image". However, one of those properties - `id` - cannot be the same for each image, and it's likely that at least one of the metadata fields (string1, string2, number1, etc) will need to vary across the images. We achieve this with _format strings_ and _increments_, explained below.

The `Composite` class has the same properties as Image (asset), plus a couple of extra ones.
Until now, the only permitted `@type` of member of a `hydra:Collection` submitted to the DLCS queue has been `Image`; this has allowed us to omit the type in the first example. 

From now however, type should be supplied (although we can assume Image if not provided).

The extra properties are:

### property: originFormat

This tells the DLCS what the source file is. Initially, the only permitted value for this property is `application/pdf`; no others are supported (but they could be later).

### property: incrementSeed

The latter affects how the DLCS assigns properties to the images it will create. It works in tandem with _format strings_ provided in the id and metadata fields. This must be an integer, for now. We can look to add other ways of formatting later.

### Format strings

The `id` property of a Composite _MUST_ contain a format string. Metadata fields (string1, string2, number1, etc) may optionally contain format strings. For any property containing a format string, the DLCS will assign a value for that property obtained by combining the format string with the incrementSeed. 


> Note: most DLCS classes have both `id` and `@id` properties. The `@id` property is the fully qualified URL of the resource, whereas the `id` property is the path component of that fully qualified resource that you are providing to make it unique within its `customer` and `space`. Keeping these separate allows domain names to change, or path syntax to change; it also allows you to submit assets to the DLCS without worrying about fully qualified identifiers for them.

An example:

```
POST /queue
```
```json
{
  "@context": "http://www.w3.org/ns/hydra/context.jsonld",
  "@type": "Collection",
  "member": [
    {
      "@type": "vocab:Composite",
      "id": "my-pdf-{0:D4}",
      "space": 6,
      "origin": "https://s3-eu-west-1.amazonaws.com/bucketname/key-path/my-pdf.pdf",
      "string1": "my-id-{0:D4}",
      "string2": "",
      "string3": "",
      "number1": "0",
      "number2": "{0}",
      "number3": "0",
      "roles": [],
      "family": "I",
      "text": "https://example.org/text/alto/my-pdf/my-pdf-{0:D4}.xml",
      "textType": "alto",
      "maxUnauthorised": -1,
      "originFormat": "application/pdf",
      "incrementSeed": 0
    }
  ]
}
```

Here, the number1-3 fields are all strings, too. They will be interpreted as numbers unless they contain an identifiable format string, such as `{0:D4}`. This syntax is borrowed from C#: see [padding](https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-pad-a-number-with-leading-zeros#to-pad-an-integer-with-a-specific-number-of-leading-zeros) and [composite formatting](https://docs.microsoft.com/en-us/dotnet/standard/base-types/composite-formatting).

Adopting these formats now seems slightly overkill, but it gives us complete flexibility to extend the formatting mechanism in future in response to emerging use cases.

If the value must itself contain a brace character (`{` or `}`), these should be escaped as [described here](https://docs.microsoft.com/en-us/dotnet/standard/base-types/composite-formatting#escaping-braces).

> Question: using .NET-specific format strings is obviously convenient for the .NET back-end, but should we look for something more universal?

#### Handling Complexity

To avoid overly complicating the `POST /queue` call, any metadata values that cannot be expressed via format strings can be amended with by making a `PATCH /customers/{customer}/spaces/{spaceId}/images/{imageId}` request to update individual images after they have been ingested.

## POST operation

If the above example is POSTED, it will return a batch just like the initial example. Assuming that *my-pdf.pdf* was a 3-page PDF, If we followed that batch's `images` property, we would get a `hydra:Collection` again, and it would look something like this:

```json
{
  "@context": "http://www.w3.org/ns/hydra/context.jsonld",
  "@id": "https://api.dlcs.io/customers/2/queue/batches/761372",
  "@type": "Collection",
  "member": [
    {
      "@type": "vocab:Image",
      "@id": "https://api.dlcs.io/customers/2/spaces/5/images/my-pdf-0000",
      "id": "my-pdf-0000",
      "service": "https://dlcs.io/iiif-img/2/5/my-pdf-0000",
      "space": 6,
      "origin": "https://s3-eu-west-1.amazonaws.com/dlcs-internal-bucket/key-path/my-pdf.pdf/my-pdf-0000.jp2",
      "string1": "my-id-0000",
      "string2": "",
      "string3": "",
      "number1": 0,
      "number2": 0,
      "number3": 0,
      "roles": [],
      "family": "I",
      "text": "https://example.org/text/alto/my-pdf/my-pdf-0000.xml",
      "textType": "alto",
      "maxUnauthorised": -1
    },
    {
      "@type": "vocab:Image",
      "@id": "https://api.dlcs.io/customers/2/spaces/5/images/my-pdf-0001",
      "id": "my-pdf-0001",
      "service": "https://dlcs.io/iiif-img/2/5/my-pdf-0001",
      "space": 6,
      "origin": "https://s3-eu-west-1.amazonaws.com/dlcs-internal-bucket/key-path/my-pdf.pdf/my-pdf-0001.jp2",
      "string1": "my-id-0001",
      "string2": "",
      "string3": "",
      "number1": 0,
      "number2": 1,
      "number3": 0,
      "roles": [],
      "family": "I",
      "text": "https://example.org/text/alto/my-pdf/my-pdf-0001.xml",
      "textType": "alto",
      "maxUnauthorised": -1
    },
    {
      "@type": "vocab:Image",
      "@id": "https://api.dlcs.io/customers/2/spaces/5/images/my-pdf-0002",
      "id": "my-pdf-0002",
      "service": "https://dlcs.io/iiif-img/2/5/my-pdf-0002",
      "space": 6,
      "origin": "https://s3-eu-west-1.amazonaws.com/dlcs-internal-bucket/key-path/my-pdf.pdf/my-pdf-0002.jp2",
      "string1": "my-id-0002",
      "string2": "",
      "string3": "",
      "number1": 0,
      "number2": 2,
      "number3": 0,
      "roles": [],
      "family": "I",
      "text": "https://example.org/text/alto/my-pdf/my-pdf-0002.xml",
      "textType": "alto",
      "maxUnauthorised": -1
    }
  ]
}
```

## Issues

The DLCS needs to unpack the PDF to, at the very least, determine the number of images it's going to have to make. It won't be able to return the batch or the batch's `images` property until it has determined how many pages the PDF has. Everything else can be deferred to processing time. In the above, the images have JP2 origins, but the JP2 isn't necessarily there yet - that's where the JP2 the DLCS needs to make for the image will be put, eventually.

The component that processes the PDF is probably a Python component. But the above flow suggests that the main API is the first recipient, because it's a member of a Collection on the queue. The API could very quickly call out to an additional component to do this unpacking, but that component would not be "front of house".

## Alternative approaches

Cantaloupe can provide an image service for any page of a PDF.
While it would mean switching to Cantaloupe as image server, this would mean NOT breaking up the PDF into images and keeping it intact. However the DLCS would need to know about the PDF forever, not just at ingest time (it's not just a wrapper to get images into the system).

This approach is attractive might might be too big a step right now.

## Development contingencies

The Deliverator API is currently being re-implemented in Protagonist, but this work will take a while to complete. We could require an alternate handler for submissions of PDFs to the queue, e.g., `/pdfqueue` or `pdf.dlcs.xxx/queue`, and have our standalone Python service process this API endpoint, unpack the PDF, and register the PDF's images using the regular, existing API.

