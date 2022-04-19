# Open Id Connect / OAuth

[RFC 005-Access-Control](./005-Access-Control.md) outlines how we can use a RoleProvider service to _authenticate_ users and allow the DLCS to _authorize_ their access to image resources.

This document follows on from this to look at how we can use oauth2/oidc as an alternative method of role provisioning.

## DLCS as Oauth 2.0 Client

The preference would be for the DLCS to be a direct _Client_ of an OAuth2.0 _Resource Server_, rather than going via a _role-provider-like_ middleman.

The DLCS "roleprovider" database record (not to be confused with RoleProvider application) will need to store configuration to both use the _Resource Server_ and map claims to DLCS roles.

### Configuration

The currently supported configuration block for "roleprovider" is:

```json
{
  "default": {
    "config": "cas",
    "target": "http://roleprovider.example.dlcs/login",
    "roles": "http://roleprovider.example.dlcs/roles",
    "logout": "http://roleprovider.example.dlcs/logout"
  }
}
```

Where:
* `_key_` is either `"default"`, or a specific hostname. If the current host matches the hostname then that config block is used, else it falls back to `"default"`.
* `config` - The type of configuration. Currently only `"cas"` is supported.
* `target` - Where the DLCS should redirect user to login.
* `roles` - URL to POST token to to fetch current user roles (the "roleprovider" row may also contain credentials in the form `{"username": "xxx", "password": "xxx"}` to use as basic-auth credentials).
* `logout` - URL to log user out and end their session.

An alternative configuration block could be (this example uses values from the Wellcome [auth-test](https://github.com/wellcomecollection/iiif-builder/tree/master/src/AuthTest) application):

```json
{
    "default": {
        "config": "oauth2",
        "domain": "<domain>.eu.auth0.com",
        "scopes": "weco:patron_role",
        "claimType": "https://wellcomecollection.org/patron_role",
        "mapping": {
            "Reader": ["https://api.dlcs.io/customers/2/roles/clickthrough"],
            "Staff": [
                "https://api.dlcs.io/customers/2/roles/clickthrough",
                "https://api.dlcs.io/customers/2/roles/clinicalImages",
                "https://api.dlcs.io/customers/2/roles/restrictedFiles"
            ]
        },
        "unknownValueBehaviour": "Error|UseClaim|Fallback",
        "fallbackMapping": ["https://api.dlcs.io/customers/2/roles/fallback"]
    }
}
```

Where:
* `_key_` is either `"default"`, or a specific hostname. If the current host matches the hostname then that config block is used, else it falls back to `"default"`.
* `config` - The type of configuration, this introduces `"oauth2"` in addition to CAS.
* `domain` - The domain of Authorization Server
* `scopes` - The custom scopes to request (assuming `openid profile` would be requested).
* `claimType` - The claim type that contains field to map
* `mapping` - A collection of `claimValue`:`dlcs-role` mappings (e.g. if user has "Reader" claim they would get clickthrough role only).
* `unknownValueBehaviour` - How to handle an unknown claim value
  * Error - throw an exception
  * UseClaim - use the claim value as-is
  * Fallback - use a default, fallback value
* `fallbackMapping` - Role(s) to use if `unknownValueBehaviour` is "Fallback"

## Useful Links

- OAuth 2.0 Roles - https://auth0.com/docs/authenticate/protocols/oauth#roles