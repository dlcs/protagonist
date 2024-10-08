# In-house Native Image Server

* Status: proposed
* Deciders: Tom Crane, Donald Gray
* Date: 2024-10-08

Issues:


## Context and Problem Statement

DLCS currently uses Cantaloupe to serve IIIF Image API requests. The performance and reliability of Cantaloupe has been unsatisfactory despite attempts to improve the situation by replacing OpenJPEG with Kakadu across our deployments and optimizing JVM settings.

The bottlenecks in Cantaloupe are primarily I/O and memory bandwidth. Stalls can be put into 2 distinct categories:

- IO stalls
- Memory stalls

IO stalls are the result of codestream data being unavailable for decoding. When Cantaloupe issues a read request to S3 it does so synchronously and decoding is blocked until that request completes.
```mermaid
sequenceDiagram
    participant Client as Client
    participant S3 as Amazon S3
    participant Decoder as Image Decoder

    Client->>Decoder: Request to decode image
    Decoder->>S3: Request image data
    Note right of Decoder: Decoder is blocked waiting for data
    S3-->>Decoder: Image data arrives
    Decoder->>Client: Decoded image
```

Memory stalls are the result of the JVM being unable to serve a memory allocation request without performing clean-up (i.e. a garbage collection event).
Since Cantaloupe has no way to constrain the memory used by native processors this leads to a tricky situation where Kakadu can cause an OOM while the JVM tries to reclaim memory by invoking the garbage collector.

```mermaid
flowchart TD
    S3([Amazon S3]) -->|Stream Data| NetworkBuffer([Network Buffer])
    NetworkBuffer -->|Copy to| DecoderBuffer([Decoder Buffer])
    DecoderBuffer -->|Pass to| Decoder([Image Decoder])
    Decoder -->|Write Decoded Data| BMPBuffer([BMP Buffer in RGBA32])
    BMPBuffer -->|Copy to| JVMBufferedImage([JVM BufferedImage])
    JVMBufferedImage -->|Copy 8x8 tiles\n or 16x16 with chroma subsampling| TurboJPEG([TurboJPEG])
    TurboJPEG -->|Copy to| Browser


subgraph Data Streaming
S3 --> NetworkBuffer
NetworkBuffer --> DecoderBuffer
DecoderBuffer --> Decoder
Decoder --> BMPBuffer
BMPBuffer --> JVMBufferedImage
JVMBufferedImage --> TurboJPEG
end
```

These stalls are the result of a less than optimal approach to handling codestream data and IO. However, they're also exacerbated by fundamental architecture decisions Cantaloupe is restricted by (e.g. being built on the JVM).

## Decision Drivers

TODO

## Considered Options

TODO

## Decision Outcome

TODO

### Positive Consequences

TODO

### Negative Consequences

TODO

## Pros and Cons of the Options

TODO