# Adjunct Access Control

Protagonist can enforce access restrictions on assets. This RFC outlines how adjuncts can be access controlled in the same fashion. 

This will require changes to both Orchestrator and IIIF-Auth-V2, these are separate services but both are involved in the auth flow. They are tightly coupled but the decision to implement a separate service is outlined in [RFC-012](./012-auth-service.md). The responsibilities for each of these are outlined below:

## IIIF-Auth-V2

IIIF-Auth-V2 is mainly responsible for implementing the [IIIF Auth 2 spec](https://iiif.io/api/auth/2.0/) and the various services outlined within. In addition to this it has 2 'off-spec' endpoints:

* `/services/` for generating IIIF Services Description resources.
* `/verifyAccess/` to verify whether the current request has access to relevant role.

IIIF-Auth-V2 isn't aware of individual assets, it makes all decisions at a customer and role level; ie _"Does this request have access to role X"_, it then records customer, not asset, based sessions; ie _"Session abc123 has 'clickthrough' for customer 10"_.

As noted above, IIIF-Auth-V2 isn't aware of individual assets and doesn't make decisions based on these. However, it does have some notion of `AssetId`. There are various paths in IIIF-Auth-V2 that accept an `assetId` value as a catch-all route parameter `{**assetId}`, internally this is mapped to an `AssetId` type. e.g.

* `/probe_internal/{**assetId}`
* `/access/{**assetId}`
* `/verifyAccess/{**assetId}`
* `/services/{**assetId}`

Generally only the `Customer` value is extracted from the `AssetId`. The exception to this is `/services/{**assetId}`, which generates full `"services"` blocks for inclusion in generated Manifests - the `"id"` contains full asset path for Orchestrator `/probe/` endpoint.

### Orchestrator 

Orchestrator is responsible for interrogating the incoming request, discovering any roles and forwarding that to IIIF-Auth-V2. There are 3 touch points between it an IIIF-Auth-V2, implemented in `IIIFAuth2Client`.

* Orchestrator has a publicly advertised `/probe/` service that proxies to IIIF-Auth-V2 `/probe_internal/`
* Calls `/services/` when constructing IIIF Manifests that contain asset restrictions.
* Calls `/verifyAccess/` on delivery hot path to check whether to process request or shortcut with 401.

## Required Changes

### Probe Service

https://iiif.io/api/auth/2.0/#probe-service

Access controlled assets have a probe service, available on path: `/auth/v2/probe/{customer}/{space}/{image}` (e.g. `/auth/v2/probe/2/10/foo`).

Serving probe service requests involves both Orchestrator and IIIF-Auth-V2.
* `{orchestrator}/auth/v2/probe/{customer}/{space}/{image}` (e.g. `{orchestrator}/auth/v2/probe/2/10/foo`) is the public URL that viewers will call. The implementation is `ProbeService`, which contains logic for shortcutting requests if they are known to fail auth, else it will make a downstream call to...
* `{iiif-auth-v2}/probe_internal/{customer}/{space}/{image}?roles={csv-roles}` (e.g. `{iiif-auth-v2}/probe_internal/2/10/foo?roles=https://dlcs.io/customers/2/roles/clickthrough`) internal endpoint. IIIF-Auth-V2 contains the logic to work out the [probe-service-response](https://iiif.io/api/auth/2.0/#probe-service-response), which the public `/auth/v2/probe/` endpoint forwards to viewers.

Access controlled adjuncts will also need a probe service, both internal and external. The paths being:
* `{orchestrator}/auth/v2/probe/{customer}/{space}/{image}/{adjunct}` (e.g. `{orchestrator}/auth/v2/probe/2/10/foo/mets.xml`)
* `{iiif-auth-v2}/probe_internal/{customer}/{space}/{image}/{adjunct}?roles={csv-roles}` (e.g. `{iiif-auth-v2}/probe_internal/2/10/foo/mets.xml?roles=https://dlcs.io/customers/2/roles/clickthrough`)

> [!TIP]
> We may be able to re-use the IIIF-Auth-V2 endpoints given they accept catch-all route parameters, which could be modelled as `DeliverableId`, rather than `AssetId`.

### Verify Access

When serving adjunct requests, Orchestrator will need to verify whether the current request can access the requested resource or not. 

Assets `*RequestHandler` paths call `IAssetAccessValidator.TryValidate()` and get an `AssetAccessResult` result back. `AdjunctRouteHandler` will need to include this in it's processing logic, if the request is `AssetAccessResult.Unauthorized` then the request is shortcut to return a 401.

> [!TIP]
> `AdjunctRequestHandler` currently has an `// TBD - AUTH` comment that outlines the approximate location where this check should happen.

#### Implementation Note

See `FileRequestHandler`, handles requests in a similar fashion to Adjuncts (ie _"serve this entire binary"_).

```cs
if (orchestrationAsset.RequiresAuth)
{
    if (!await IsAuthenticated(assetRequest, orchestrationAsset, httpContext.Request))
    {
        logger.LogDebug("User not authenticated for {Method} {Path}", httpContext.Request.Method,
            httpContext.Request.Path);
        return new StatusCodeResult(HttpStatusCode.Unauthorized);
    }
}
```

### Asset and Adjunct Roles

At the time of writing Adjuncts do not have roles of their own, only Assets do. We can implement roles logic incrementally, this will be fully expanded in individual issues but the rough order being

1. Adjuncts don't have their own Roles. If parent Asset has a Role, Adjunct inherits this (MVP).
2. Adjunct have their own Roles. These can differ from Asset, or allow parent Asset to have roles while the Adjunct doesn't
   * TBD whether the latter will be allowed, it would allow for scenarios like _"You can read the transcript but not listen to the recording"_.

The cached `OrchestrationAdjunct` will need to include a Roles property to aid decision making.

## Manifest Generation

Orchestrator makes downstream requests to IIIF-Auth-V2 when constructing Manifests that contain assets with roles. The returned auth services are included on IIIF Presentation v3 single-item and named-query Manifests.

The `@context` includes `auth/2/` context. The top Manifest level `"services"` element contains full `AuthAccessService2` service description and individual `"services"` contain an `AuthProbeService2` that include a reference to the already included `AuthAccessService2` (ie it doesn't render everything again).

All included adjuncts will need to include an `"AuthProbeService2"`. Example below:

```jsonc
{
    "@context": [
        "http://iiif.io/api/auth/2/context.json", // /auth/2/ context
        "http://iiif.io/api/presentation/3/context.json"
    ],
    "id": "https://dlcs.example/iiif-manifest/v3/7/1/foo",
    "type": "Manifest",
    "label": {
        "en": [
            "Generated by DLCS"
        ]
    },
    "services": [
        {
            // Full auth service description, other services reference by Id
            // From IIIF-Auth-v2
            "id": "https://dlcs.example/auth/v2/access/7/clickthrough",
            "type": "AuthAccessService2",
            "profile": "active",
            "label": {
                "en": [
                    "Sample clickthrough label"
                ]
            },
            "service": [
                {
                    "id": "https://dlcs.example/auth/v2/access/7/token",
                    "type": "AuthAccessTokenService2"
                },
                {
                    "id": "https://dlcs.example/auth/v2/access/7/clickthrough/logout",
                    "type": "AuthLogoutService2",
                    "label": {
                        "en": [
                            "Log out of session"
                        ]
                    }
                }
            ],
            "confirmLabel": {
                "en": [
                    "Accept and Open"
                ]
            },
            "heading": {
                "en": [
                    "Sample clickthrough note"
                ]
            },
            "note": {
                "en": [
                    "<p>This is a test of clickthrough via IIIF Authorization Flow 2.0</p><ul><li>this</li><li>is</li><li>html</li><li>content.</li></ul>By agreeing to this you should have access to asset."
                ]
            }
        }
    ],
    "items": [
        {
            "id": "https://dlcs.example/iiif-img/7/1/foo/canvas/c/1",
            "type": "Canvas",
            "label": {
                "en": [
                    "Canvas 1"
                ]
            },
            "width": 3771,
            "height": 5279,
            "seeAlso": [
                {
                    "id": "https://dlcs.example/adjuncts/7/1/foo/mets.xml",
                    "type": "Dataset",
                    "profile": "http://www.loc.gov/standards/alto/v3/alto.xsd",
                    "label": {
                        "en": [
                            "METS-ALTO XML"
                        ]
                    },
                    "format": "text/xml",
                    "service": [
                        {
                            // New. Probe service containing reference to main service
                            "id": "https://dlcs.example/auth/v2/probe/7/1/foo/mets.xml",
                            "type": "AuthProbeService2",
                            "service": [
                                {
                                    "id": "https://dlcs.example/auth/v2/access/7/clickthrough",
                                    "type": "AuthAccessService2"
                                }
                            ]
                        }
                    ]
                }
            ],
            "rendering": [
                {
                    "id": "https://dlcs.example/adjuncts/7/1/foo/alternative.jpg",
                    "type": "Image",
                    "label": { "en": [ "A different JPEG of the asset" ] },
                    "format": "image/jpeg",
                    "service": [
                        {
                            // New. Probe service containing reference to main service
                            "id": "https://dlcs.example/auth/v2/probe/7/1/foo/mets.xml",
                            "type": "AuthProbeService2",
                            "service": [
                                {
                                    "id": "https://dlcs.example/auth/v2/access/7/clickthrough",
                                    "type": "AuthAccessService2"
                                }
                            ]
                        }
                    ]
                }
            ],
            "items": [
                {
                    "id": "https://dlcs.example/iiif-img/7/1/foo/canvas/c/1/page",
                    "type": "AnnotationPage",
                    "items": [
                        {
                            "id": "https://dlcs.example/iiif-img/7/1/foo/canvas/c/1/page/image",
                            "type": "Annotation",
                            "motivation": "painting",
                            "body": {
                                "id": "https://dlcs.example/iiif-img/7/1/foo/full/350,490/0/default.jpg",
                                "type": "Image",
                                "width": 350,
                                "height": 490,
                                "format": "image/jpeg",
                                "service": [
                                    {
                                        "@context": "http://iiif.io/api/image/3/context.json",
                                        "id": "https://dlcs.example/iiif-img/v3/7/1/foo",
                                        "type": "ImageService3",
                                        "profile": "level2",
                                        "width": 3771,
                                        "height": 5279,
                                        "service": [
                                            {
                                                // Existing. Probe service containing reference to main service
                                                "id": "https://dlcs.example/auth/v2/probe/7/1/foo",
                                                "type": "AuthProbeService2",
                                                "service": [
                                                    {
                                                        "id": "https://dlcs.example/auth/v2/access/7/clickthrough",
                                                        "type": "AuthAccessService2"
                                                    }
                                                ]
                                            }
                                        ]
                                    },
                                    {
                                        // Existing. Probe service containing reference to main service
                                        "id": "https://dlcs.example/auth/v2/probe/7/1/foo",
                                        "type": "AuthProbeService2",
                                        "service": [
                                            {
                                                "id": "https://dlcs.example/auth/v2/access/7/clickthrough",
                                                "type": "AuthAccessService2"
                                            }
                                        ]
                                    }
                                ]
                            },
                            "target": "https://dlcs.example/iiif-img/7/1/foo/canvas/c/1"
                        }
                    ]
                }
            ]
        }
    ]
}
```