# Access Control

## Context

This document is about access control for assets and services for those assets, not for the APIs of the DLCS itself.
This means the IIIF Auth API, in its current and future versions, rather than the REST HTTP API used to create and administer the assets. The IIFI API is comsumed (indirectly) by end-users, via IIIF Clients in the browser. The REST API is consumed by systems integration workflows, tools, and possibly browser-based content creation tools.

## Providing the IIIF Auth API

The DLCS is the system serving the assets, therefore it must establish a session (e.g., by using cookies, or other means) for the user.

When images are registered, they are given a set of *roles*. These are usually opaque URIs as far as the DLCS is concerned. An open image has no roles, anyone can see it.

The DLCS can enforce the IIIF *Clickthrough* pattern without further integration, as it does not need to know the identity of the user. It just needs to establish a session for them, and ensure they have accepted any clickthrough terms.

All other kinds of auth require the DLCS to know what roles the user has, and to establish a session for them so that it can authorise their access to images based on their known roles - if the image they are trying to access has the role, then the user's session needs to have the role.

The system that knows who the user is, and can tell the DLCS what roles the user has, might be an external single-sign-on system. The DLCS implements a very simple protocol for *role acquisition* (it still doesn't need to actually know who the user is, unlike many other single-sign-on requirements).

This protocol is not dependent on the IIIF Auth API, and the DLCS can accomodate future changes in that spec, or support the provision of alternative auth arrangements, using the role-acquisition integration described here.

## 

Role provider configuration - by space? By individual image? By customer?

In this first example, the Role Provider application presents the login form, takes the user's credentials, and queries some store of user data with those credentials. This is a simpler flow, but may not be how a large institional single sign on system works.

![Direct auth](sequence-src/auth-direct.png "Direct Auth")

The second flow assumes that the role provider application is a client service of an insitutional single sign on system (just as the library catalogue, or e-Learning platforms, might be). In this case the Role Provider doesn't present a login UI, but it still renders HTTP responses that redirect or close windows.

![Auth with SSO](sequence-src/auth-sso.png "Auth with SSO")



## TODO

### Session identity

Although the DLCS doesn't need to know who the user is to enforce access control (just their roles), it might still be useful to include some (probably anonymised) identity token in the user's session. This allows the DLCS to log access, and possibly later enforce quotas. The data could be reconciled by reporting outside of the DLCS.