# Text Services Integration

* Status: decided
* Deciders: Tom Crane, Donald Gray
* Date: 2026-06-03

## Context and Problem Statement

The new [`text-services`](https://github.com/dlcs/text-services) service supports a suite of functionality that will be of use to the wider IIIF-CS.

It supports 2 APIs
* Builder - builds binary text index and a variety of stored derivatives per job.
* Search - publicly serves generated artefacts through IIIF-standard endpoints.

Some key points about both APIs:
* Neither have any form of authentication, they are both open.
* Neither are specific to IIIF-CloudServices, they are both completely stand alone. ie neither has a concept of Customer or Space etc.

This ADR isn't an exhaustive look at how we will use `text-services` in IIIF-CS; instead it is a record of some broad rules we'll use when integrating within the wider platform.

## Decision Drivers

* Flexibility of integration. We will be integrating with it in a number of ways (e.g. IIIF Presentation for augmenting Manifests, Protagonist for PDF generation).
* Maintaining IIIF CloudServices concepts. As noted above, `text-services` is ignorant of IIIF-CS, we need to integrate in such a way that we enforce these concepts within the platform.

### Summary 

See below summary, split by each API.

#### Builder

This should be a private service, access restricted internally. There can be no public access to it. Any jobs created must be done via another service that keeps track of identifiers.

#### Search

This will be publicly available on the same hostname as Orchestrator. From a consumer perspective they are consuming a single service.

### Pros and Cons

#### Positive Consequences

* Search being on the same hostname as Orchestrator is familiar and consistent.
* Builder being private service means it can remain ignorant to IIIF-CS concerns, these are applied at a level further back.

#### Negative Consequences

* Small but possible risk of job-id collisions if multiple services sharing same builder.
* Potentially code/wrapper overhead for exposing builder API to public. If we wanted to expose in Protagonist API there would effectively be some publicly available pass-through endpoints that add little, possibly segregating job-ids by Customer or enforcing job-ids must start with `{customer}/` (dependent on how it is exposed).