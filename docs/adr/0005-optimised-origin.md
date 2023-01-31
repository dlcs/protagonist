# Optimised Origin

* Status: proposed
* Deciders: Donald Gray, Tom Crane
* Date: 2023-01-30

Issue: [#64](https://github.com/dlcs/protagonist/issues/64)

## Context and Problem Statement

The DLCS currently has the notion of a customerOriginStrategy. This dictates how assets can be fetched (over HTTP, via S3-cli, HTTPs etc) and any credentials required.

It also has an "optimised" flag which conflates 2 things:
* assets are tile-ready JP2s (so don't need transcoded)
* assets can be treated as own storage, no need to copy source image to DLCS.

We need to split these out to be different things.

## Decision Drivers

* Flexibility - we should be able to have assets that are tile-ready AND/OR storage that can be treated as own.
* Simplicity - should be easy to work out how an asset will be handled in the DLCS.

## Decision Outcome

The splitting of customerOriginStrategy is addressed below.

### Optimised Storage
The "optimised" column on the CustomerOriginStrategy now only indicates that the asset can be treated as the DLCS' own storage (i.e. it is fast and stable enough to stream `/file/` resources from and orchestrate images from).

"optimised" should be restricted to `s3-ambient` origins.

> If "optimised" can be applied to anything other than s3-ambient origin strategies then this adds additional complexity when using SpecialServer. SpecialServer is ignorant of CustomerOriginStrategy - it doesn't know how to access something via HTTPS with credentials. S3-ambient negates this as the same IAM policy can be applied to Orchestrator and SpecialServer.

### Tile-Ready
To indicate that an asset is image-server ready (could be a small jpeg, a tile-optimsed JPEG2000 or a Pyramidal TIFF) we will introduce a new imageOptimisationPolicy with a key of `use-original`. `use-original` is only valid for image assets. We may want to restrict use of `use-original` by customer.

The below shows the combinations of "Optimised" + imageOptimisationPolicy and the affects in various parts of system (`fast-higher` iop is used to represents anything other than `use-original`).

| Optimised | IOP            | Engine                                                                  | `/iiif-img/`                  | `/file/`                 |
| --------- | -------------- | ----------------------------------------------------------------------- | ----------------------------- | ------------------------ |
| false     | `fast-higher`  | Download from origin, convert to JP2(+thumbs), save in DLCS-storage     | Orchestrate from DLCS Storage | Stream from DLCS Storage |
| true      | `fast-higher`  | Download from origin, convert to JP2(+thumbs), save in DLCS-storage     | Orchestrate from DLCS Storage | Stream from Origin       |
| false     | `use-original` | Download from origin to create thumbs. Copy from origin to DLCS Storage | Orchestrate from DLCS Storage | Stream from DLCS Storage |
| true      | `use-original` | Download from origin to create thumbs.                                  | Orchestrate from Origin       | Stream from Origin       |

### Additional Change - Ordering
In addition to above we will introduce a "priority" field to customerOriginStrategy table. This will allow control over the order in which strategies are checked and should simplify more complex rules, such as those configured for Wellcome.

#### Positive Consequences

* Easier to understand.
* More control over which assets are "tile-ready", allows customers to use their own profiles etc.

#### Negative Consequences

* Leaks some of the internal implementation details of DLCS to API consumer.
* Incorrectly using `use-original` for huge image could put unnecessary load on Cantaloupe.
* Changing meaning of DB field so will need to run a DB update on deployment. Depending on usage we may need to check customerOriginStrategy Regex against Origin's to determine imageOptimisationPolicy (ie may not fit as a single migration).
