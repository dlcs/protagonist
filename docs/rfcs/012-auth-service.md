# Separate Auth Service

The DLCS currently supports [IIIF Auth 1.0](https://iiif.io/api/auth/1.0/) and, to a lesser extent, [IIIF Auth 0.9](https://iiif.io/api/auth/0.9/). These tables are modelled directly in the main DLCS database.

## Auth2 Service

This RFC looks at introducing a new, separate auth service for supporting [IIIF Auth 2.0](https://iiif.io/api/auth/2.0/). The main drivers for this are:

* It avoids making migrations to the existing DLCS database by introducing a new datastore. 
* We need to manage a lot more values for IIIF Auth v2 compared to v1.
* Will give us more flexibility for introducing new auth services moving forward.
* Separation of concerns - auth functionality self contained.

This will be a self contained service with it's own datastore, with the following functionality:

* API supporting CRUD operations on the various auth service properties.
* Handling all IIIF Auth 2.0 interactions (access service, access token service, logout, probe service)
* User session and auth-token management
* Validation whether a session has access to a role
* Generating auth service IIIF Presentation definitions to add to IIIF manifests.
* Handling OIDC interactions with 3rd customer auth services.

## Interactions

All auth functionality currently resides in the main DB and these tables are used by Orchestrator to handle all interactions. By moving these to a separate service there'll be some additional service-service interactions.

### Orchestrator

Orchestrator will need to call the AuthService to generate the IIIF service descriptions, see below for API details. 

These will be appended to info.json, single-asset manifests and named-query projections - with the `"id"` property updated where required (this is already happening for auth 0.9 + 1.0 services). This works in a simiar way to how info.json is fetched from Cantaloupe and updated.

## Database

All AccessService configuration and display values will be stored in the AuthService database.

We will support languages for property values, with multiple strings per language.

## Backwards Compatibility

Add flag for returning 401 for info.json. 

Orchestrator still handles auth 1.0 + 0.9

## Implementing Auth Spec

This section addresses the various points in the Auth spec and addresses how we will handle these

The following endpoints will be required, either in Orchestrator or the new Auth Service

### [Access Service](https://iiif.io/api/auth/2.0/#access-service)

> The access service either grants the authorizing aspect or determines whether the user already has that aspect.

Proposed endpoints:
* AuthService `GET /auth/v2/{customer}/{access-service}?origin={origin}`
  * Where auth-service is the name of the configured access-service in the database.
  * Only `active` profile will be supported initially.

This request will initiate a login request for the specified `{access-service}`. The full flow will be determined by the AccessService config in the database, see [RFC 008 - More Access Control](008-more-access-control-oidc-oauth.md).

The AuthService will render some user interface component for the user to make a gesture on. The contents of this will be driven by values saved in the database. 

The AuthService will render it's own UI for both `clickthrough` and external OAuth provider. The process for each will slightly differ:
* `clickthrough` - the AuthService will render a message in UI and credentials are not required. Once the user has confirmed/agreed to terms we can create a session, set a access cookie and exit.
* OAuth - the DLCS will use [`Authorization Code Flow`](https://oauth.net/2/grant-types/authorization-code/) and follow the sequence diagram in [RFC 008](008-more-access-control-oidc-oauth.md). The returned claims will be parsed to a role and a session created for those roles.
  * If the `?origin=` value is the same as the domain where the DLCS is being hosted we won't need to show a confirmation step. However, if they differ we need to get the user to carry out a significant gesture in the DLCS domain, so we will need to render some UI. The text values for this will be stored in database.

### [Access Token Service](https://iiif.io/api/auth/2.0/#access-token-service)

> The access token service is used by the client to obtain an access token, which it then sends to a probe service.

Proposed endpoints:
* AuthService `GET /auth/v2/{customer}/token`

### [Probe Service](https://iiif.io/api/auth/2.0/#probe-service)

> The probe service is used by the client to understand whether the user has access to the access-controlled resource for which the probe service is declared. The client sends the token obtained from the corresponding access token service to the probe service.

Proposed endpoints:
* Orchestrator `GET /auth/v2/probe/{asset-id}` (e.g. `GET /auth/v2/probe/10/20/foo`)
  * Only the orchestrator knows the relationship between an asset + roles - the AuthService couldn't answer this on it's own.
  * Responses can be shortcut from here - for example if the image doesn't require auth, or an access token hasn't been passed via `Authorization` header.
  * If the asset requires auth, Orchestrator delegates to AuthService to generate the [Probe Service Reponse](https://iiif.io/api/auth/2.0/#probe-service-response) and sends it to client unchanged.
* AuthService `GET /probe/{asset-id}?role={csv-roles}` (e.g. `GET /probe/10/20/foo?role=gold,silver`)
  * The AuthService maintains a list of sessions and can validate that the provided token has access to the requested role. The response can be fully generated as it doesn't have any hostname specific `"id"` values etc.
  * This is on a different path to avoid any DLCS general routing - we may want to add some restrictions to make it only accessible to Orchestrator (via IP or some other auth mechanism).

This is detailed below:

![Probe Service](sequence-src/012-probe-svc.png "Probe Service")

An alternative implementation would be to have the above relationship reversed, with AuthService being the entry point and it calling out to Orchestrator, or API, to get a list of roles for an image. This keeps the Auth responsibilities purely in the AuthService _but_ we lose the ability to shortcut responses as we would always need to get a list of roles. Image:Role could be cached but Orchestrator first keeps this cleaner.

`"substitution"` will not be supported at this point.

### [Logout Service](https://iiif.io/api/auth/2.0/#logout-service)

> In the case of the active access service pattern, the client may need to know if and where the user can go to log out.

* AuthService `GET /auth/v2/{customer}/{access-service}/logout`

A 'logout' operation will result in the underyling user session being either deleted, or marked as invalid. If a 3rd party was involved in authenticating the user we won't log them out of that service.

## API

### Management 

The AuthService will have CRUD operations to manage all stored resources.

### Service Description

The GET operations for AccessService will IIIF services descriptions that can be added to IIIF API Resources. This will be publicly available to render services description elements. The `"id"` value returned will follow default DLCS paths only and would need to be rewritten if alternative paths were used. The endpoint won't verify that an image exists, it will take an image and role and return the service description, e.g.

`GET /auth/v2/service/{asset-id}?role={roles}`, e.g. `GET /auth/v2/service/10/20/foo?role=gold`

```json
{
  "service": [{
    "id": "https://dlcs.example/v2/10/20/foo",
    "type": "AuthProbeService2",
    "service": [{
      "id": "https://dlcs.example/v2/10/gold",
      "type": "AuthAccessService2",
      "profile": "active",
      "label": { "en": ["Login page"] },
      "note": { "en": ["You will be redirected to login"] },
      "service": [{
        "id": "https://dlcs.example/auth/v2/10/token",
        "type": "AuthAccessTokenService2"
      },{
        "id": "https://dlcs.example/v2/10/gold/logout",
        "type": "AuthLogoutService2",
        "label": { "en": [ "Logout of session" ] }
      }]
    }]
  }]
}
```

### Authentication

Delegate auth to DLCS, similar to Composite-Handler (for now!)

## Questions

* When Orchestrator calls the ProbeService - do we want to pass roles in a header, rather than query parameter? The former wouldn't be logged. Is Image:Role relationship sensitive?
* Should the `/probe/` endpoint in AuthService be locked down?
* How do we handle the different paths? Could this be an unnecessary complication?
* Do Orchestrator and AuthService need be on the same domain? e.g. If Orchestrator is on `dlcs.example` and auth on `auth.dlcs.example` - would that lead to cookie issues? Thumbs service works in this way to may be a non-issue.
* How do we know if the auth to be added to an image is 1 or 2 (or 3 etc in future)? Would we need something in the main db? Or a setting flag? Or would it be a different role?