# Orchestrator Proxy Rules

* Status: decided
* Deciders: Tom Crane, Donald Gray
* Date: 2026-02-20

## Context and Problem Statement

With the implementation of ADR [0010-replace-maxunauthorised](0010-replace-maxunauthorised.md) all images have an effective `maxWidth` value - either explicitly set or falling back to system default. 

This protects the overall service and allows us to reject incoming image requests at the Orchestrator level before proxying them to downstream image-service. 

Until now we have relied on the downstream image-service to apply size restrictions via whatever means they support. IIP Image supports [`MAX_CVT`](https://iipimage.sourceforge.io/documentation/server#configuration), which is the equivalent of `maxWidth`. Cantaloupe has [`max_pixels`](https://cantaloupe-project.github.io/manual/5.0/deployment.html#limiting) which is the equivalent of `maxArea`. We now have a single `maxWidth` property which maps directly to IIIF `maxWidth` - how do we enforce this limit at Orchestrator level without slowing down requests or altering image-serving behaviour?

## Decision Drivers

* Performance - we cannot slow down Orchestrator image handling.
* Predictable behaviour between image servers.

## Decision Outcome

### Summary 

Orchestrator will always rewrite the IIIF size parameter when proxying `/full/`, `/max/` or `/!w,h/` size requests to specify the exact size. This will ensure we can consistently apply `maxWidth` behaviour without worrying about downstream image-server configuration (with the caveat that the image-server must be able to handle the potential `maxWidth` values).

Orchestrator already interrogates all incoming image requests. To date it has checked incoming `full` region requests to see if they exceed `maxUnauthorised`, and all requests to determine which service to proxy them to (thumbs, special-server or image-server). This check is straightforward as we only need to know the incoming IIIF size parameter and image dimensions to make these decisions. However, we now need to calculate the size of the IIIF region parameter and include these in decision making. 

Protagonist supports ImageApi v2.1 and v3.0. Both of these have slightly different supported image request parameters. We will implement "strict" and "lax" handling if Image requests in Orchestrator. The former will be the default, with the latter allowing us to avoid breaking changes upon release.

> [!WARNING]
> We have noted that version of Cantaloupe being used doesn't strictly follow some IIIF Image parameters so we will start with "strict" mode but may need to enable "lax".

The rules for strict and lax are as below:

**Strict**

| ImageApiVersion | RequestSize                            | Region         | ProxySize or Result                                                |
| --------------- | -------------------------------------- | -------------- | ------------------------------------------------------------------ |
| 2.1             | `/full/`                               | lte `maxWidth` | `/w,h/` where w + h is extracted region                            |
| 2.1             | `/full/`                               | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  |
| 3.0             | `/full/`                               | *              | 400 BadRequest - `/full/` not valid                                |
| *               | `/^full/`                              | *              | 400 BadRequest - `/^full/` never valid                             |
| 2.1             | `/max/`                                | lte `maxWidth` | `/w,h/` where w + h is extracted region scaled up to `maxWidth`    |
| 2.1             | `/max/`                                | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  |
| 3.0             | `/max/`                                | lte `maxWidth` | `/w,h/` where w + h is extracted region                            |
| 3.0             | `/max/`                                | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  |
| 2.1             | `/^max/`                               | *              | 400 BadRequest - `^` not valid                                     |
| 3.0             | `/^max/`                               | lte `maxWidth` | `/w,h/` where w + h is extracted region                            |
| 3.0             | `/^max/`                               | gt `maxWidth`  | `/^w,h/` where w + h is extracted region scaled up to `maxWidth`   |
| 2.0             | Any with `^`                           | *              | 400 BadRequest - `^` not valid                                     |
| *               | `/!w,h/` where max(w,h) lte `maxWidth` | *              | `/w,h/` where w + h is calculated exact size                       |
| *               | `/!w,h/` where max(w,h) gt `maxWidth`  | *              | `/w,h/` where w + h is the largest possible size within `maxWidth` |
| *               | _other_                                | lte `maxWidth` | Pass size through                                                  |
| *               | _other_                                | gt `maxWidth`  | 400 BadRequest - `^` not valid                                     |

**Lax**

The lax rules are identical to above, with the exception that we support `/full/` use for v3.0. i.e. we will still reject `^` for 2.0 requests.

| ImageApiVersion | RequestSize                            | Region         | ProxySize or Result                                                | Change from Strict   |
| --------------- | -------------------------------------- | -------------- | ------------------------------------------------------------------ | -------------------- |
| 2.1             | `/full/`                               | lte `maxWidth` | `/w,h/` where w + h is extracted region                            |                      |
| 2.1             | `/full/`                               | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  |                      |
| 3.0             | `/full/`                               | lte `maxWidth` | `/w,h/` where w + h is extracted region                            | Treated like `/max/` |
| 3.0             | `/full/`                               | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  | Treated like `/max/` |
| *               | `/^full/`                              | *              | 400 BadRequest - `/^full/` never valid                             |                      |
| 2.1             | `/max/`                                | lte `maxWidth` | `/w,h/` where w + h is extracted region scaled up to `maxWidth`    |                      |
| 2.1             | `/max/`                                | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  |                      |
| 3.0             | `/max/`                                | lte `maxWidth` | `/w,h/` where w + h is extracted region                            |                      |
| 3.0             | `/max/`                                | gt `maxWidth`  | `/w,h/` where w + h is extracted region scaled down to `maxWidth`  |                      |
| 3.0             | `/^max/`                               | lte `maxWidth` | `/w,h/` where w + h is extracted region                            |                      |
| 3.0             | `/^max/`                               | gt `maxWidth`  | `/^w,h/` where w + h is extracted region scaled up to `maxWidth`   |                      |
| 2.0             | Any with `^`                           | *              |                                                                    |                      |
| *               | `/!w,h/` where max(w,h) lte `maxWidth` | *              | `/w,h/` where w + h is calculated exact size                       |
| *               | `/!w,h/` where max(w,h) gt `maxWidth`  | *              | `/w,h/` where w + h is the largest possible size within `maxWidth` |
| *               | _other_                                | lte `maxWidth` | Pass size through                                                  |                      |
| *               | _other_                                | gt `maxWidth`  | 400 BadRequest - `^` not valid                                     |                      |

### Pros and Cons

#### Positive Consequences

* Gives more control to Orchestrator, meaning we will have more consistent behaviour. Will afford changing downstream image-server without worrying about any potential 'quirks' affecting public requests, or whether they apply `maxArea` or `maxWidth` etc.
* Allow rejection of images at Orchestrator level, less overall work done.
* Will hopefully save image-server from doing calculations for `/full/` and `/max/` if we work out intended sizes upfront (will need proven via tests).
* Should make cropping/sizes more predictable as we'll be doing more size calculation in Orchestrator using `iiif-net` lib (but not for _all_ sizes).

#### Negative Consequences

* Introduces more IIIF ImageRequest parsing logic to Orchestrator.
* Doing more work per image request. This should all be quick calculations but overall it is still more work.
* Not necessarily a negative but with this change we're updating core path functionality of Orchestrator that has been stable for a long time. Will need thorough testing!
* Downstream image-server configuration needs to be able to handle any `maxWidth` specified by Orchestrator.