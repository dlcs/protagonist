# Replace `maxUnauthorised`

* Status: decided
* Deciders: Tom Crane, Donald Gray
* Date: 2026-01-22

## Context and Problem Statement

> [!NOTE]
> All sizes below are "on longest edge"

Protagonist currently has a single `maxUnauthorised` property to control the availability of image sizes. This property pre-dates the IIIF spec and it's intention was to allow anonymous users to view full thumbnails of restricted content. Current behaviour in is:
* `maxUnauthorised` = -1, any user can view any size and any region if they have required role<sup>*</sup>
* `maxUnauthorised` >= 0, regardless of whether Image has a role, means:
  * `/full/` requests up to and including that size work for anonymous users (if 0 then anonymous users cannot see any size)
  * Anonymous users cannot see any non `/full/` region, regardless of the requested size (ie deep-zoom doesn't work)
  * If the image also has a role, and the requesting user has that role, they can view all sizes and all regions (so, if there is no role then no-one can view non-full regions or `/full/` regions larger than `maxUnauthorised` value).

_<sup>*</sup>There may be a `"maxArea"` set at the image-server level to avoid overloading resources. The exact value will be dependant on image-server configuration but will be in megapixel range. We don't check this in Orchestrator. Also, Deliverator didn't have any `null` values so `-1` represents `null`_

> [!NOTE]
> The 2nd bullet, above, was changed between Deliverator and Protagonist. In the former `maxUnauthorised` was only used when an image had an associated role. Now it is applied whether the image has a role or not.

To afford greater control for image availability, e.g. add size restrictions whilst supporting non-full requests, `maxUnauthorised` alone is no longer enough. This ADR outlines how we can replace `maxUnauthorised` to allow more flexible behaviour. 

See original ticket: https://github.com/dlcs/protagonist/issues/306

### Current Behaviour

The below tables shows the effect of different `maxUnauthorised` and `role` values for different systems:

#### Orchestrator

| `maxUnauthorised` | `role` | Request Type                    | Result                         | Description                                                                       |
| ----------------- | ------ | ------------------------------- | ------------------------------ | --------------------------------------------------------------------------------- |
| -1                | `null` | *any*                           | 200                            | All sizes/regions available                                                       |
| -1                | *any*  | *any*                           | 200 if user has role, else 401 | No sizes/regions available to anonymous                                           |
| 0                 | `null` | *any*                           | 401                            | No sizes/regions available                                                        |
| 0                 | *any*  | *any*                           | 200 if user has role, else 401 | No sizes/regions available to anonymous                                           |
| 700               | `null` | `/full/` region, size <=700     | 200                            | `full` < `maxUnauthorised` available anonymously                                  |
| 700               | `null` | non-`/full/` region, size <=700 | 401                            | Non `full` images unavailable. Need to attain `role` to view but there is no role |
| 700               | *any*  | `/full/` region, size <=700     | 200                            | `full` < `maxUnauthorised` available anonymously                                  |
| 700               | *any*  | non-`/full/` region, size <=700 | 200 if user has role, else 401 | Non `full` images unavailable anonymously. Can be viewed if have `role`           |
| 700               | `null` | `/full/` region, size >700      | 401                            | Need to attain `role` to view but there is no role                                |
| 700               | `null` | non-`/full/` region, size >700  | 401                            | Need to attain `role` to view but there is no role                                |
| 700               | *any*  | `/full/` region, size >700      | 200 if user has role, else 401 | Size > `maxUnauthorised` so need `role` to view                                   |
| 700               | *any*  | non-`/full/` region, size >700  | 200 if user has role, else 401 | Non `full` images unavailable anonymously. Can be viewed if have `role`           |

#### Engine

`maxUnauthorised` and `role` also play a part in thumbnail _storage_, not _generation_ as we always generate all thumbs but their 'open' or 'auth' location will differ, see [RFC 001-Thumbnails](https://github.com/dlcs/protagonist/blob/develop/docs/rfcs/001-thumbnails.md).

| `maxUnauthorised` | `role` | Thumbnails                            | Notes                                                        |
| ----------------- | ------ | ------------------------------------- | ------------------------------------------------------------ |
| -1                | `null` | All thumbnails 'open'                 |                                                              |
| -1                | *any*  | All thumbnails 'open'                 | This is inconsistent with Orchestrator. Should all be 'auth' |
| 0                 | `null` | All thumbnails 'auth'                 |                                                              |
| 0                 | *any*  | All thumbnails 'auth'                 |                                                              |
| 700               | `null` | Thumbnails <= 700 'open', else 'auth' |                                                              |
| 700               | *any*  | Thumbnails <= 700 'open', else 'auth' |                                                              |

> [!NOTE]
> `maxUnauthorised` and `role` also have a bearing on whether images are included in PDF projections. For simplicity this hasn't been included in ADR but will need to be addressed in implementation.

#### Thumbs

As detailed in linked RFC, thumbnail service will only serve 'open' thumbnails.

## Decision Drivers

* Flexibility - `maxUnauthorised` only works for `/full/` region requests, we need a new, more flexible approach to allow us to meet alternative use cases.
* Backwards compatibility - the new implementation cannot break any existing Protagonist uses of `maxUnauthorised`.

## Decision Outcome

`maxUnauthorised` will be superseded by 2 new properties `maxWidth` and `openFullMax`, these are detailed below:

### New Properties

Both of these values treat anything <= 0 as unset, meaning they are assigned "system default" behaviour. What that behaviour is differs dependant on property, details below:

#### [`maxWidth`](https://deploy-preview-2--dlcs-docs.netlify.app/api-doc/asset#maxwidth)

From documentation:
> Restricts the maximum permitted pixel response as defined in the Image API.
>
> The platform only supports `maxWidth`, not `maxHeight` or `maxArea`.
> 
> The [IIIF Image API specification](https://iiif.io/api/image/3.0/#52-technical-properties) says "If maxWidth is specified and maxHeight is not, then clients should infer that maxHeight = maxWidth." 
> In the IIIF Cloud Services platform, `maxWidth` therefore defines a square bounding box. So you can't get a 100 w x 1000 h image if `maxWidth` is 100.
> 
> As well as governing the allowed pixel responses from the level 2 `iiif-img` delivery channel, this value is also considered by the `thumbnail` delivery channel. 
> The platform will not generate thumbnails larger than the maxWidth limit even if the policy defines them.
>
> If the image has roles, and openFullMax and openMaxWidth are both 0, no thumbnails will be produced (this behaviour is not dependent on maxWidth but on the requirement that nothing on the thumbnail delivery channel is subject to access control; 
> if you offer thumbs they must be accessible anonymously).

`maxWidth` applies to _all_ requests regardless of the region. It is not possible to make a request larger than this size, regardless of any other image property.

The handling of `maxWidth` is the same regardless of the image's `maxWidth`. If the image has a value `> 0` then that specific value is used, falling back to the system-default. 

Attempting to set an image-specific `maxWidth` that exceeds the system-default will result in a 400|BadRequest. 

Orchestrator will check _all_ incoming image requests and will reject if they exceed the `maxWidth`. This may require looking up image dimensions and calculating resulting size (e.g. for `max`, `^max`, `pct:n` and `^pct:n` size parameters). Engine will use to determine which thumbnails to generate.

The calculated `maxWidth` value will be included on all info.json files. If `maxHeight` or `maxArea` is set by image-server this will be overridden.

> [!CAUTION]
> The system-default `maxWidth` in Orchestrator will need to roughly align with the downstream image-server, e.g. `MAX_CVT` for IIPImage or `max_pixels` in Cantaloupe.
>
> This affords a layer of protection to prevent Orchestrator forwarding requests that could overwhelm downstream image-servers, with the additional benefit that it could avoid unnecessary orchestrations.
>
> We could remove the equivalent setting from downstream image-server and allow Orchestrator to manage the restriction alone but it still seems a safe option to maintain the 2nd layer of protection.

#### [`openFullMax`](https://deploy-preview-2--dlcs-docs.netlify.app/api-doc/asset#openfullmax)

From documentation:
> Only applies when an image has roles, and the image request region is `/full/`. 
> This value is ignored if the image does not have roles (it is not considered an error, as you may vary roles for other reasons). 
> Pixel requests on the `iiif-img` delivery channel whose region is `/full/` and whose `size` fits a bounding square `openFullMax` on an edge are permitted _whether the use has any of the roles or not_ (therefore also for anonymous users).
>
> On the `thumbnail` delivery channel, thumbs will be generated by the policy as long as their resulting sizes are equal to or less than the bounding square defined by openFullMax.
>
> This setting allows an access-controlled image to have open thumbnails. Typical settings are low - e.g., 200 or 400 - sizes suitable for thumbnails but not for reading text in an image.
> 
> A value of 0 or less than 0 is considered unset.

Unlike `maxWidth`, a value of `<= 0` is effectively setting this value of `0`, meaning that there are no anonymously accessible full region images - _but only if there is a `role`_.

> [!IMPORTANT]
> If `openFullMax` > `maxWidth` then in effect `openFullMax` == `maxWidth`.

### Property Permutations

The below table outlines the different permutations and their effect on request handling:

#### Orchestrator

| `maxWidth` | `openFullMax` | `role` | Request Type                              | Result                         | Description                                   | Notes                                                                              |
| ---------- | ------------- | ------ | ----------------------------------------- | ------------------------------ | --------------------------------------------- | ---------------------------------------------------------------------------------- |
| 0          | 0             | `null` | *any*                                     | 200                            | No restrictions, all available                | system-default `maxWidth` still applies                                            |
| 0          | 0             | *any*  | *any*                                     | 200 if user has role, else 401 | No sizes/regions available to anonymous       |                                                                                    |
| 700        | 0             | `null` | Any region, size <= 700                   | 200                            | Any size <= `maxWidth` is available           |                                                                                    |
| 700        | 0             | *any*  | Any region, size <= 700                   | 200 if user has role, else 401 | No sizes/regions available to anonymous       |                                                                                    |
| 700        | 0             | `null` | Any region, size > 700                    | 401                            | No size > `maxWidth` is available             | `maxWidth` applied under all circumstances                                         |
| 700        | 0             | *any*  | Any region, size > 700                    | 401                            | No size > `maxWidth` is available             | `maxWidth` applied under all circumstances                                         |
| 0          | 400           | `null` | *any*                                     | 200                            | No restrictions, all available                | `openFullMax` ignored as no `role`, system-default `maxWidth` still applies        |
| 0          | 400           | *any*  | `/full/` region, size <= 400              | 200                            | `full` <= `openFullMax` available anonymously | Available to all under `openFullMax`                                               |
| 0          | 400           | *any*  | `/full/` region, size > 400               | 200 if user has role, else 401 | `full` > `openFullMax`so need `role` to view  |                                                                                    |
| 0          | 400           | *any*  | non-`/full/` region, any size             | 200 if user has role, else 401 | Non `/full/` so need `role` to view           |                                                                                    |
| 700        | 400           | `null` | Any region, size <= 700                   | 200                            | Any size <= `maxWidth` is available           | `openFullMax` ignored as no `role`                                                 |
| 700        | 400           | `null` | Any region, size > 700                    | 401                            | No size > `maxWidth` is available             | `openFullMax` ignored as no `role`                                                 |
| 700        | 400           | *any*  | `/full` region, size <= 400               | 200                            | `full` <= `openFullMax` available anonymously |                                                                                    |
| 700        | 400           | *any*  | `/full` region, size > 400 and <= 700     | 200 if user has role, else 401 | `full` > `openFullMax`so need `role` to view  | Size is above `openFullMax` but below `maxWidth`                                   |
| 700        | 400           | *any*  | `/full` region, size > 700                | 401                            | No size > `maxWidth` is available             | `maxWidth` applied under all circumstances                                         |
| 700        | 400           | *any*  | non-`/full` region, size <= 400           | 200 if user has role, else 401 | No non-`full` region available to anonymous   |                                                                                    |
| 700        | 400           | *any*  | non-`/full` region, size > 400 and <= 700 | 200 if user has role, else 401 | No non-`full` region available to anonymous   | Size is above `openFullMax` but below `maxWidth`                                   |
| 700        | 400           | *any*  | non-`/full` region, size > 700            | 401                            | No size > `maxWidth` is available             | `maxWidth` applied under all circumstances                                         |
| 500        | 800           | `null` | Any region, size <= 500                   | 200                            | Any size <= `maxWidth` is available           | `openFullMax` ignored as no `role`                                                 |
| 500        | 800           | `null` | Any region, size > 500                    | 401                            | No size > `maxWidth` is available             | `openFullMax` ignored as no `role`                                                 |
| 500        | 800           | *any*  | `/full` region, size <= 500               | 200                            | `full` <= `maxWidth` available anonymously    |                                                                                    |
| 500        | 800           | *any*  | `/full` region, size > 500                | 401                            | No size > `maxWidth` is available             | `maxWidth` applied under all circumstances. The larger `openFullMax` has no effect |
| 500        | 800           | *any*  | non-`/full` region, size <= 500           | 200 if user has role, else 401 | No non-`full` region available to anonymous   |                                                                                    |
| 500        | 800           | *any*  | non-`/full` region, size > 500            | 401                            | No size > `maxWidth` is available             |                                                                                    |

#### Engine

| `maxWidth` | `openFullMax` | `role` | Thumbnails                            | Notes                            |
| ---------- | ------------- | ------ | ------------------------------------- | -------------------------------- |
| 0          | 0             | `null` | All thumbnails 'open'                 |                                  |
| 0          | 0             | *any*  | All thumbnails 'auth'                 |                                  |
| 700        | 0             | `null` | Thumbnails <= 700 'open', else 'auth' |                                  |
| 700        | 0             | *any*  | All thumbnails 'auth'                 |                                  |
| 0          | 400           | `null` | All thumbnails 'open'                 | No role so `openFullMax` ignored |
| 0          | 400           | *any*  | Thumbnails <= 400 'open', else 'auth' |                                  |
| 700        | 400           | `null` | Thumbnails <= 700 'open', else 'auth' | No role so `openFullMax` ignored |
| 700        | 400           | *any*  | Thumbnails <= 400 'open', else 'auth' |                                  |
| 500        | 800           | `null` | Thumbnails <= 500 'open', else 'auth' | No role so `openFullMax` ignored |
| 500        | 800           | *any*  | Thumbnails <= 500 'open', else 'auth' |                                  |

#### Thumbs

No change - will continue to only serve 'open' thumbnails.

### Use Case / Behaviours

Below is a summary of use-cases from Orchestrator PoV; how these are currently modelled and what the equivalent using new properties would be:

* All sizes/regions available
  * Was: `maxUnauthorised=-1,role=null`.
  * Now: `maxWidth=0,openFullMax=0,role=null` OR `maxWidth=0,openFullMax=1000,role=null`
* No sizes/regions available to any user
  * Was: `maxUnauthorised=0,role=null`
  * Now: `maxWidth=0,openFullMax=0,role="https://dlcs.io/roles/unobtainable"`
* No sizes/regions available to anonymous user, logged in users can see any size
  * Was: `maxUnauthorised=0,role="foo"` OR `maxUnauthorised=-1,role="foo"`
  * Now: `maxWidth=0,openFullMax=0,role="foo"`
* `/full/` requests up to 1000px available to anonymous. Deep zoom unavailable to any user.
  * Was: `maxUnauthorised=1000,role=null`
  * Now: `maxWidth=0,openFullMax=1000,role="https://dlcs.io/roles/unobtainable"`
* `/full/` requests up to 1000px available to anonymous. Deep zoom and larger sizes available to logged in user.
  * Was: `maxUnauthorised=1000,role="foo"`
  * Now: `maxWidth=0,openFullMax=1000,role="foo"`
* Any request up to 1000px available to all users.
  * Was: _not possible_
  * Now: `maxWidth=1000,openFullMax=0,role=null`

> [!IMPORTANT]
> The above highlights a key difference with new properties and the need to use a 'fake' role in some scenarios.
> The current implementation allows an image to be effectively unavailable to anyone by setting `maxUnauthorised=0,role=null`. 
> We cannot directly replicate this - there needs to be a "fake" unobtainable role used to get equivalent behaviour, this will be `https://dlcs.io/roles/unobtainable`

### Implementation / Rollout Considerations

The above 2 properties will be saved in DB, replacing `maxUnauthorised` value. To avoid any rollout issues we should have an initial, interim migration that adds `maxWidth` and `openFullMax` without removing `maxUnauthorised`. 
This will allow us to to a safe rolling deployment. We can follow up with a migration to remove `maxUnauthorised` shortly after.

The initial migration to add the new columns will need to migrate data, as well as changing schema. We must not break any existing behaviour.

We will continue accepting `maxUnauthorised` in API payloads but these will be mapped to `maxWidth` and/or `openFullMax` to maintain current behaviour as above.

All info.json files will need to be regenerated.