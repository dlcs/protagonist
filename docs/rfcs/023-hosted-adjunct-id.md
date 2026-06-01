# Annotation Adjuncts `id` Property

This RFC looks at how to ensure that the `id` property is correct, i.e. it represents the current, configuration-specific
location (e.g. affected by path rewrite).

This is specific to `iiifLink` of `annotation` only. The API user has specified that the
adjunct has `"type": "AnnotationPage"`, and it is _expected_ that the adjunct binary content is a valid `AnnotationPage`.

> [!NOTE]
> There is currently no verification at any point that this content indeed conforms to the expectations

## Proposed Solutions Overview

Two contrasting solutions were proposed:

1. Write the `id` value when saving the adjunct to S3
2. Rewrite the `id` value when outputting requested adjunct

This RFC will argue for the option of `id` rewrite on-the-fly. This is primarily due to a need to handle scenarios with `s3-ambient`
stored items. As those are immutable, and hence they ultimately require _some_ processing on the output, they make development effort
for the solution `2` required in both cases, and therefore much more sensible and a general solution.

The main downsides that will likely need either addressing or at least observation is the performance hit that comes with
output manipulation vs serving pre-modified content.

## Implementation Overview

When working on the `iiif-presentation` a similar scenario has been encountered before, and a `StreamingJsonProcessor` class
has been created. It is a static implementation of the .NET `System.Text.Json`-based JSON processor which operates on
a stream, without e.g. loading entire contents into memory, allowing it to work on arbitrarily large JSON documents.

Similarities with previous scenario continue, as `iiif-presentation` also includes a "plugin" class for the
aforementioned `StreamingJsonProcessor` specifically designed to modify the `id` property of a JSON document.

Therefore, uplifting both the processor and the `id`-rewrite plugin into Protagonist would allow us
to completely reuse the code, speeding up resolution of this issue.

Using this processor the remaining implementation steps are as follows:
* Identify that the request is for an `annotation`/`inline-annotation` adjunct
* Determine the correct `id` value
* Obtain the S3 stream response within Orchestrator and stream into JSON processor
* Stream the processor output to user response, returning a 5xx exception should the binary be unprocessable.

> [!NOTE]
> Decision is to internalise `StreamingJsonProcessor`, rather than creating a dedicated NuGet package.
>
> The classes are relatively small and the additional overhead of maintenance (build and pipelines etc)> as deemed
> to not be worth it. We can revisit in the future should circumstances change.

## Downsides and Considerations

Proposed approach is not free from downsides and trade-offs, the main one being that Orchestrator becomes part
of the "content streaming pipeline", which despite optimizations already present in the `StreamingJsonProcessor` it
will always be slower and incur more cloud resource usage than just proxy response.

This RFC argues that the code-reuse and relatively low effort of implementation make this still the best option,
because as mentioned before, the `s3-ambient` scenario still would require essentially identical code path to
be implemented. Therefore, an attempt to use this code path for all relevant requests gives us chance of observing if
the potential performance issues indeed realise (and warrant further development) or not, resolving the issue.

## Content Validation

As an addendum to this RFC, a related matter of ensuring that the `AnnotationPage`-type adjuncts actually conform
to the expected JSON shape could be tackled.

Validation should happen during ingestion (of a hosted adjunct) in the Engine. Likely, the simplest and most reliable
way of ensuring that the JSON conforms would be to load and deserialise it to the appropriate IIIF model.

The main issue/consideration is the need to load the entire JSON into memory, which in most cases should not cause problems.
However, as technically the JSON size is not constrained, it could be either used as an attack vector against IIIF Cloud Services
deployment, or in non-malicious cases possibly degrade performance, which might be especially problematic on shared environments.

Solution to this would be implementation of limits of the annotation adjunct size. The options are:
* Size limit of ingest
* Size limit of verification 

### Size limit of ingestion

This option is to put a hard limit over the size of the hosted annotation adjunct that's allowed to be created
and served. This mainly would exist as a protection against malicious JSON submission, where a crafted JSON of
an enormous size could cause Orchestrator to drastically increase both inbound and outbound bandwidth, and also
CPU usage, if forced into repeatedly pushing very large amounts of valid JSON through the streaming processor.

### Size limit of verification

After upload to S3 finishes (or it's determined to exist in `s3-ambient` bucket), byte length is
obtained from S3. If it exceeds a globally configured value, the supposed annotation page JSON is
not loaded, and if it's invalid it will have to be caught at a different point, possibly by the user.

This in itself doesn't protect from the streaming processor abuse for Orchestrator, but rather protects Engine
from potentially running out of memory or just having degraded performance.

### Size Limits Summary

Both of the aforementioned limits could be set, a smaller one that limits "Engine validates IIIF AnnotationPage JSON" step,
and a (possibly much) larger one that's there to combat hypothetical malicious actors. 
