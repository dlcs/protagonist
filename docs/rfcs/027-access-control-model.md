# Access Control Model: Role Combinations, IP-Restrictions and Appointments

[RFC 012](012-auth-service.md) introduced IIIF-Auth-V2 as a separate service for [IIIF Auth 2.0](https://iiif.io/api/auth/2.0/), and [RFC 008](008-more-access-control-oidc-oauth.md) added OIDC as a way of obtaining roles. This RFC proposes how IIIF-Auth-V2 and Orchestrator model roles, the providers that grant them, and the access services they emit, so that they support:

* **IpRestriction access**: access from known IP ranges (e.g. a reading room), with no sign-in.
* **Appointments**: a key that grants access to a set of assets for a period of time. The assets are determined at request time via a pattern.
* **Role Combinations**: an asset whose access needs more than one thing, e.g. an appointment *and* being in the reading room, or signing in *and* accepting a content warning.
* **Several ways in to one asset**, e.g. staff sign-in *or* an appointment, presented sensibly in the IIIF.
* **Per-reason probe wording**: telling the user *why* they can't see an item and what would help.

The reasoning behind this RFC, from a customer perspective, is explored in https://github.com/uol-library/design/blob/main/access-control/RFCs/dynamic-roles.md. This RFC explores how we can model those requirements in the platform, without shaping specifically around a single customer's use cases.

> [!NOTE]
> This was worked through with a mock DLCS in [iiif-auth-ux-prototype](https://github.com/dlcs/iiif-auth-ux-prototype), which demos the user experience in several viewers. Its [model doc](https://github.com/dlcs/iiif-auth-ux-prototype/blob/docs/iiif-auth-v2-model/docs/iiif-auth-v2-model.md) and [scenario walkthrough](https://github.com/dlcs/iiif-auth-ux-prototype/blob/docs/iiif-auth-v2-model/docs/iiif-auth-v2-scenarios.md) take all 15 combinations of OIDC, IP, clickthrough and appointment through every call in the auth flow, with the SQL each runs. This RFC summarises the approach and the changes required; the walkthrough has the detail.

## Current State

As outlined in [RFC 024](024-adjunct-access-control.md), Orchestrator and IIIF-Auth-V2 meet at three points: `/services/` (building IIIF services descriptions), `/probe_internal/` (proxied by Orchestrator's public `/auth/v2/probe/`) and `/verifyAccess/` (on the delivery hot path).

Today:

* An asset's `roles` are a flat list, and **any one** role grants access.
* Each `Role` has a single `AccessServiceId`: the "primary" service rendered in IIIF for that role. The services description for an asset is the distinct set of its roles' primary services.
* Each `AccessService` has a single `RoleProviderId`, and role providers are either `Clickthrough` or `Oidc`.
* `ClickThroughProviderHandler` grants the roles whose `AccessServiceId` is the clickthrough's access service.
* A new grant creates a new `SessionUser`, **replacing** any roles from an earlier one ([iiif-auth-v2#10](https://github.com/dlcs/iiif-auth-v2/issues/10) covers handling uses with existing session).
* Orchestrator's probe returns 401 "Missing credentials" without calling IIIF-Auth-V2 if there's no bearer token, and image requests return 401 without calling `/verifyAccess/` if there's no Auth2 cookie.
* Only the `Cookie` header is propagated to IIIF-Auth-V2. It never sees the client's IP.
* The probe's `heading` and `note` are generic ("Missing credentials", "Session does not have required roles").

None of these support location or appointment access, and the probe can't explain what a user would need.

## Concepts

The proposal separates four things that today's entities partly merge:

| Concept           | Question it answers                                | Today                              | Proposed                                                                |
| ----------------- | -------------------------------------------------- | ---------------------------------- | ----------------------------------------------------------------------- |
| **Role**          | What does an asset require?                        | `Role` (URI) + `AccessServiceId`   | `Role` (URI, with a kind prefix) + `RoleProviderId`                     |
| **RoleProvider**  | How can someone obtain roles?                      | `Oidc`, `Clickthrough`             | + `IpRestriction`, `Appointment`. Its configuration says what it grants |
| **AccessService** | What appears in the IIIF, with what wording?       | one provider, via `RoleProviderId` | a *set* of providers, via `AccessServiceRoleProvider`                   |
| **Appointment**   | Which assets can someone with a key see, and when? | —                                  | `Appointment`, with a time window and asset patterns                    |

### Granting vs offering

One provider can grant many roles, and one role can be granted by many providers. But *able to grant* is not the same as *should be offered*:

* **Granting** lives in provider configuration: OIDC claim mapping, IP CIDR ranges, a clickthrough's roles. It has to, as an OIDC provider with `UnknownValueBehaviour = UseClaim` can grant an open-ended set of roles.
* **Offering** is `Role.RoleProviderId`: the one provider whose access service is emitted for assets carrying that role.

For example, a `clickthrough` role on 1,000 images is offered by the Clickthrough provider. An Entra provider used for 100 staff-only images has a claim mapping that grants both `staff` *and* `clickthrough`. So:

* The 900 clickthrough-only images only offer the clickthrough, never a staff sign-in.
* A member of staff who has signed in with Entra can view the clickthrough images without accepting the agreement: the probe finds `clickthrough` in their session and returns 200.

This works because there is one token service per customer (`/auth/v2/access/{customer}/token`), shared by every access service. The client sends the token it got through Entra when it probes a clickthrough image.

If a role ever needs to be offered by more than one provider (e.g. `staff` via both Okta and Entra), give each provider its own role (`staff-okta`, `staff-entra`) and add both to the asset as separate entries. This keeps `Role.RoleProviderId` single-valued.

> [!NOTE]
> Providers are *instances*, not types. Two OIDC configurations (e.g. Okta and Entra) are two providers, with different login pages and different roles. A provider with several host-specific configurations (`RoleProviderConfiguration` host keys) is still one provider.

### Role names and prefixes

Roles remain URIs. The last path segment carries a prefix that says what kind of role it is:

| Prefix         | Example                                                     | Provider type        | Checked                                                                  |
| -------------- | ----------------------------------------------------------- | -------------------- | ------------------------------------------------------------------------ |
| `ip:`          | `https://api.dlcs.io/customers/2/roles/ip:reading-room`     | IpRestriction        | on every request, from the client IP                                     |
| `appointment:` | `https://api.dlcs.io/customers/2/roles/appointment:xa-1234` | Appointment          | on every request, from the session's appointments and the asset's fields |
| *(none)*       | `https://api.dlcs.io/customers/2/roles/staff`               | Oidc or Clickthrough | from the session's roles                                                 |

A role's kind still comes from its provider. The prefix mirrors it so that Orchestrator, which only has an asset's role URIs, can act without a lookup:

* **`ip:`** tells Orchestrator when to call IIIF-Auth-V2 even though the request has no token or cookie.
* **`appointment:`** tells Orchestrator to send the asset's metadata fields (`space`, `scopes`, `tags`, `string1`–`3`, `number1`–`3`) so appointment patterns can be matched.

IIIF-Auth-V2 validates on write that the prefix matches the provider type: `ip:` roles point at an IpRestriction provider, `appointment:` roles at an Appointment provider, and unprefixed roles at an Oidc or Clickthrough provider.

### Role entries: AND / OR

This follows the role combination section of the dynamic roles RFC. Short role names are used below; each stands for a full role URI.

Today, an asset's `roles` are a flat list and **any one** role grants access. That can't express a combination. Suppose an asset is for CC&G Protected Access level 2 users anywhere, *or* any CC&G staff member but only in a CC&G office. A flat list:

```json
{ "roles": [ "ccg-pa-2", "ip:ccg-offices", "ccg-all-staff" ] }
```

lets in any CC&G staff member from anywhere, and anyone at all on the office network. Instead, `roles` becomes a list of **entries**, and an entry can join several roles with `+`:

```json
{ "roles": [ "ccg-pa-2", "ip:ccg-offices+ccg-all-staff" ] }
```

* **OR across entries:** the request must match **any one** entry.
* **AND within an entry:** to match an entry, the request must have **every** role in it.

So this reads "`ccg-pa-2`, *or* both `ip:ccg-offices` and `ccg-all-staff`". A single-role entry behaves as today, so existing assets need no change.

Any kind of role can be combined: OIDC, clickthrough, `ip:` or `appointment:`. More generally, all of these entries are valid together:

```json
{ "roles": [ "role1+role2", "role1+role3", "role4", "role5+role6+role3" ] }
```

But this set is **inconsistent**, and is rejected when the asset is saved:

```json
{ "roles": [ "role1+role2", "role1+role3", "role3" ] }
```

It says `role3` on its own is enough, but also that `role1+role3` is needed. The general rule: no entry may contain all the roles of another entry. If it does, the larger entry can never be the only one a request matches, so it will be rejected.

The running example from the dynamic roles RFC is **asset A**:

```json
{ "roles": [ "ccg-pa-2", "ccg-pa-4", "ip:reading-room+appointment:token-based" ] }
```

Three entries, any of which will do: `ccg-pa-2`, *or* `ccg-pa-4`, *or* an appointment used in the reading room. For each request, every entry is evaluated, and every role in an entry is checked in its own way: OIDC and clickthrough roles from the session, `ip:` roles from the client IP, and `appointment:` roles from the session's appointments and the asset. The probe uses the same evaluation to choose between 200, 401 and 403 (see [Probe](#probe)).

> [!NOTE]
> Appointments have AND/OR logic of their own, in their asset patterns: AND across fields, OR within a field (see [Appointments](#appointments)). That decides whether one `appointment:` role is met for an asset; it's separate from the AND/OR between an asset's roles.

### Storing and transmitting role entries

> [!TIP]
> **Suggestion** 
> 
> How entries are stored and passed between services is open. The approach below is the recommended way to do it, with alternatives listed.
>
> When implementing we may find a compelling reason to use an alternative so I've listed them here.

Today, `roles` is a `string[]` in the API. In the database it's `Images.Roles`, a `varchar(1000)` that an EF value converter joins with commas, and which Orchestrator's Dapper query (`DapperColumnMapping.SplitDelimited`) and CSV ingest split on commas. It's passed to IIIF-Auth-V2 as `?roles=uri1,uri2`. So commas already mean "or", and role URIs are already assumed to contain no commas.

**Storage and API: strings with `+`**, as in the dynamic roles RFC:

```json
{ "roles": [ "ccg-pa-2", "ip:ccg-offices+ccg-all-staff" ] }
```

* No change for assets with single-role entries, no schema change, and CSV ingest keeps working.
* The `+` is evident what this is (you need _"ip:ccg-office and ccg-all-staff"_).
* **Restrict the characters in role names.** The DLCS saves role URIs, so IIIF-Auth-V2 can reject `+`, `,`, `:` and whitespace in role names when roles are created, and Protagonist checks the same rule when an asset is saved. The separators are then safe.
* **Widen `Images.Roles`** to `text` (or well beyond 1000 characters), as several entries of full URIs soon add up.
* Protagonist validates entries when an asset is saved: every role is known, no duplicates, and entries are consistent (see above).
* A shared `RoleEntry` type (`Parse`/`ToString`) keeps the API, ingest and Orchestrator agreeing on the format.

Alternative approaches:

| Option               | API `roles`                                         | Database                                                      | For                                                      | Against                                                                                                   |
| -------------------- | --------------------------------------------------- | ------------------------------------------------------------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| **Nested arrays**    | `["ccg-pa-2", ["ip:ccg-offices", "ccg-all-staff"]]` | `jsonb` `[["ccg-pa-2"], ["ip:ccg-offices", "ccg-all-staff"]]` | Structural, so no separators. Queryable with a GIN index | Mixed types are awkward in Hydra and typed clients. CSV ingest still needs a text form. Needs a migration |
| **Normalised table** | either                                              | `AssetRoleEntries(asset, entry, role)`                        | Easiest to query by role                                 | A join on every asset read, including Orchestrator's hot path                                             |

**Between services, structured rather than text.** A literal `+` in a query string is decoded as a space by ASP.NET, so `?roles=a,b+c` would silently become `b c` unless every `+` is sent as `%2B`. Orchestrator should parse the stored form once and send entries structured:

* **`/probe_internal/` and `/verifyAccess/`: `POST` with a JSON body.** These now need more than roles anyway (the asset's metadata fields, for appointments; the client IP stays in `X-Forwarded-For`):

  ```json
  {
    "roles": [ [ "https://api.dlcs.io/customers/2/roles/ccg-pa-2" ],
               [ "https://api.dlcs.io/customers/2/roles/ip:ccg-offices", "https://api.dlcs.io/customers/2/roles/ccg-all-staff" ] ],
    "asset": { "space": 1, "scopes": [ "b76hr45e" ], "tags": [], "string1": "LIDDLE/WW2/HFIU/003", "string2": "", "string3": "", "number1": 0, "number2": 0, "number3": 0 }
  }
  ```

* **`/services/`: stays `GET`**, with one entry per repeated parameter, roles in an entry separated by commas and each role URL-encoded: `?entry=…%2Fccg-pa-2&entry=…%2Fip%3Accg-offices,…%2Fccg-all-staff`. Keeping it a `GET` lets the result be cached by URL (see [Caching services descriptions](#caching-services-descriptions)).

## Role Providers

| Type                  | Grants                                                                      | Stored in the session | Checked                         |
| --------------------- | --------------------------------------------------------------------------- | --------------------- | ------------------------------- |
| `Clickthrough`        | the roles whose `RoleProviderId` is this provider                           | `SessionUser.Roles`   | from the session                |
| `Oidc`                | claim -> role mapping (or `UseClaim`)                                       | `SessionUser.Roles`   | from the session                |
| `Appointment` (new)   | the appointment's role, for assets matching its patterns, within its window | `SessionAppointment`  | per request                     |
| `IpRestriction` (new) | the `ip:` roles whose `RoleProviderId` is this provider, when the client IP is in its ranges | nothing | per request, from the client IP |

For Clickthrough and IpRestriction providers, "the roles it grants" and "the roles it's offered for" are the same set, so the provider grants the roles whose `RoleProviderId` is itself, and no role list is needed in its configuration. `ClickThroughProviderHandler` reads `RoleProviderId` accordingly.

### IpRestriction

An IpRestriction provider is **one place**, which may have several ranges. Its configuration lists the CIDR ranges and gives a place name for wording (e.g. "the Research Centre"):

```json
{
  "default": {
    "config": "IpRestriction",
    "ranges": [ "10.0.0.0/24", "2001:db8:1007::/48" ],
    "placeName": "the Research Centre"
  }
}
```

* A request from one of the ranges has every `ip:` role whose `RoleProviderId` is this provider. Usually that's one role (`ip:reading-room`). If several roles should be granted by the same ranges, they all point at the same provider.
* Each place is its own provider (reading room, campus, head office), which is also what the `placeName` wording and the choice between `external` services (see [Which access services an asset gets](#which-access-services-an-asset-gets)) rely on.
* IpRestriction roles are never stored in a session: being on site needs no sign-in, so no session or token is required. IIIF-Auth-V2 matches the client IP on each probe and `verifyAccess` request.

#### IpRestriction providers grant only `ip:` roles

An IP range can't also grant a role of another kind, e.g. `10.0.0.0/24` granting `clickthrough` as well as `ip:reading-room`. Unlike an OIDC provider granting extra roles, this would clash with Orchestrator's shortcut:

* Orchestrator only calls IIIF-Auth-V2 without credentials when the asset has an `ip:` role. An asset with just `[clickthrough]` has none, so a reading-room user with no token gets 401 from Orchestrator, and IIIF-Auth-V2 never sees their IP.
* Image requests are the same: with no cookie and no entry made entirely of `ip:` roles, Orchestrator rejects locally.
* So the extra grant would only work for users who already have a session, which is inconsistent. Making it work generally would mean calling IIIF-Auth-V2 for every protected asset, losing the shortcut that the prefixes exist for.

(An OIDC provider's extra roles are stored in the session, and Orchestrator has a token or cookie to pass on, so it isn't affected.)

The same need is met with asset role entries instead. _"Reading-room users don't need to accept the warning"_ is:

```json
{ "roles": [ "clickthrough", "ip:reading-room" ] }
```

Orchestrator's rules then work as they are, an `external` service is emitted (the IP entry works on its own), and the probe can say "Accept the warning, or view it in the Research Centre". The cost is adding the entry to the affected assets.

### Appointments

An appointment is a key that grants one or more `appointment:` roles, for assets matching its patterns, between `start` and `end`. It follows the dynamic roles RFC and the [appointments app](https://github.com/uol-library/design/blob/main/access-control/appointments-app.md), whose API manages them:

```json
{
  "token": "a5rf65ne99pa",
  "roles": [ "https://api.dlcs.io/customers/2/roles/appointment:token-based" ],
  "start": "2026-10-20T10:00:00Z",
  "end": "2026-10-20T16:00:00Z",
  "active": true,
  "approvedBy": "reading-room-staff@example.org",
  "name": "Dana Okafor",
  "assetPatterns": {
    "customer": [ 2 ],
    "scopes": [ "b76hr45e" ],
    "string1": [ "LIDDLE/WW2/HFIU" ]
  }
}
```

* **Entering the key** (`token`) on the appointment access service page adds a `session_appointments` row, creating the session if needed. The key is accepted if the appointment exists and hasn't ended. One that hasn't started, or isn't yet `active` (awaiting approval), is accepted too, and the probe explains why it doesn't work yet. The appointment's roles are not added to `SessionUser.Roles`.
* **On every request**, an `appointment:` role R is met if the session holds an appointment that:
  * has R in its `roles`
  * is `active`
  * has `start ≤ now < end`
  * has `assetPatterns` that match the asset

  Asset patterns are AND across fields and OR within a field, and a null or empty field places no constraint. `string1`–`3` match exactly or as a `/`-separated descendant, so `LIDDLE/WW2/HFIU` matches `LIDDLE/WW2/HFIU/001` but not `LIDDLE/WW2/HFIUX`. `scopes` and `tags` match if any one of the asset's values matches. `customer`, `space` and `number1`–`3` must be equal.
* Because these conditions are checked per request, **sessions aren't tied to the appointment's end**. Access stops at `end` regardless, and other roles in the same session (e.g. from signing in) aren't cut short. Cancelling or deactivating an appointment takes effect at the next check, subject to caching.
* "Only in the reading room" is an asset entry that needs both an `appointment:` and an `ip:` role (`ip:dlip-reading-room+appointment:token-based`). It isn't a property of the appointment.

#### Example: the appointment access service

The Appointment role provider needs little configuration of its own, because the appointments carry the detail:

```json
{
  "default": {
    "config": "Appointment",
    "gestureTitle": "Use your appointment key",
    "gestureMessage": "Click to continue to your appointment"
  }
}
```

An `access_services` row (name `appointment`, profile `active`), linked to that provider in `access_service_role_providers`, is rendered by `/services/` as:

```json
{
  "id": "https://dlcs.example/auth/v2/access/2/appointment",
  "type": "AuthAccessService2",
  "profile": "active",
  "label": { "en": [ "Appointment key" ] },
  "heading": { "en": [ "Viewing by appointment" ] },
  "note": { "en": [ "Enter the key from your appointment confirmation." ] },
  "confirmLabel": { "en": [ "Enter key" ] },
  "service": [
    { "id": "https://dlcs.example/auth/v2/access/2/token", "type": "AuthAccessTokenService2" },
    {
      "id": "https://dlcs.example/auth/v2/access/2/appointment/logout",
      "type": "AuthLogoutService2",
      "label": { "en": [ "End appointment session" ] }
    }
  ]
}
```

An asset whose only interactive role is an `appointment:` role gets this service. Asset A also has OIDC entries (`ccg-pa-2`, `ccg-pa-4`), so it gets the service offering both Entra and Appointment instead, whose page offers a choice between them (see [Which access services an asset gets](#which-access-services-an-asset-gets)).

Dana's visit to the reading room then goes:

1. **Probe, no token, from the reading room.** Asset A has an `ip:` role, so Orchestrator calls `/probe_internal/` with `X-Forwarded-For`. The reading room IP gives `ip:dlip-reading-room`. The third entry still needs `appointment:token-based`, which Dana can get by entering a key, so the probe returns **401**: "Sign in, or enter your appointment key".
2. **Access service.** Dana opens `/access/2/appointment` (or the choice page) and enters `a5rf65ne99pa`. The appointment is found, active and within its window, so IIIF-Auth-V2 creates a session, adds the `session_appointments` row, sets the cookie and closes the tab.
3. **Token.** The client gets a token from `/access/2/token`, using the cookie.
4. **Probe, with the token.** Orchestrator sends the token, `X-Forwarded-For` and the asset's metadata fields. `ip:dlip-reading-room` is met by the IP. `appointment:token-based` is met because the asset's `string1`, `LIDDLE/WW2/HFIU/003`, is under `LIDDLE/WW2/HFIU` and the asset is in scope `b76hr45e`. The third entry is met, so the probe returns **200**.
5. **The same session on an asset outside the patterns**, e.g. `string1 = LIDDLE/WW2/EVAC/014`, gets **403** "Not part of your appointment". From home, it gets **403** "Only available in the Research Centre".

> [!NOTE]
> Appointments are proposed as a table in IIIF-Auth-V2. An alternative is for the Appointment provider's configuration to point at an external booking system, which IIIF-Auth-V2 queries.

## Access Services

An access service offers a *set* of providers:

* **One OIDC provider**: the page redirects straight to the IdP, as today.
* **Appointment**: the page is a key entry form.
* **Clickthrough**: no content, as today. The client has already shown the terms from the service's `heading`/`note`.
* **Several interactive providers** (e.g. Entra and Appointment): the page offers a choice, e.g. "Sign in with Entra" or "Enter an appointment key".

URLs still use the access service's `name` (`/access/{customer}/{name}`). The page can't know which asset the user came from, so a choice page may offer a route that can't work for that asset. Only the probe's note can say which will work.

### Which access services an asset gets

`/services/` decides from the asset's roles:

1. The asset's roles -> the distinct set of their `RoleProviderId`s.
2. **`external`**: emitted only if an entry is made **entirely** of `ip:` roles, i.e. being on site is enough on its own. When IP is only part of an entry, the `external` flow can't satisfy it, so it isn't emitted and the probe explains the location requirement.
3. **Clickthrough**: always one service per Clickthrough provider, as each carries its own terms.
4. **Everything else** (the interactive set: Oidc, Appointment) -> **one** `active` service. That's the service whose provider set exactly matches, or else the smallest service whose set contains it. A customer has a consolidated service offering all its interactive providers, so there is always a match. Services for smaller sets are optional and only refine the wording.

So an asset gets at most one `external`, one clickthrough (unless it needs several) and one other `active` service. A client that only uses the first `active` service (e.g. Theseus, via react-iiif-vault) still offers every interactive route.

Rules enforced on write:

* IpRestriction providers are linked only to `external` services, and `external` services contain only IpRestriction providers.
* A Clickthrough service offers exactly one provider.
* The customer has a service offering all its interactive providers.
* A role's prefix matches its provider's type.

> [!NOTE]
> Matching a provider set to a service can be done in memory: build a `Dictionary<providerKey, AccessService>` (sorted provider ids) when the customer's access services are loaded into the existing per-customer cache. Uniqueness of provider sets is checked by the API on write.

### Caching services descriptions

> [!TIP]
> **Suggestion, not a requirement.** This reduces calls to `/services/`, but nothing in the model depends on it.

Today Orchestrator calls `/services/{customer}/{space}/{image}?roles=…`, and IIIF-Auth-V2 returns the whole probe service, including the probe's `id` (the asset's Orchestrator probe URL, from `ToProbeService`). So every asset's URL and response differ, even though the access services inside are identical for every asset with the same roles. `ManifestV3Builder` calls `GetAuthServices` once per canvas, with no caching, so a 100-canvas manifest makes 100 calls.

The suggestion is to split the response, so the part that can be shared has a URL without the asset:

* **IIIF-Auth-V2** returns only the access services for a customer and a set of role entries: `GET /services/{customer}?entry=…&entry=…` -> `[ AuthAccessService2, … ]`. This depends only on the customer, the entries and the customer's configuration.
* **Orchestrator** wraps the list in the `AuthProbeService2` itself, with the asset's probe `id`. Orchestrator owns the probe endpoint, so it already knows the path.
* **Cache by URL.** Orchestrator builds the URL in a normalised form (roles sorted within each entry, entries sorted, duplicates removed) so that `[a, b+c]` and `[c+b, a]` share a result. It caches responses in memory (`IAppCache`), keyed by URL. A manifest whose 100 canvases share roles then makes one call. An HTTP cache in front of IIIF-Auth-V2 could do the same.
* **Freshness.** Services change only when access services, providers or roles are edited, which is rare. A TTL of a few minutes is probably enough; info.json caching already accepts that kind of delay. If edits must show immediately, IIIF-Auth-V2 could add a per-customer configuration version to the response or the URL.

### OAuth callbacks

The callback is `/access/{customer}/{name}/oauth2/callback`. An access service offering several providers needs to know which one the user chose, so `role_provision_tokens` gains `role_provider_id`, set when sign-in starts. Each access service that includes an OIDC provider has its own callback URL, which must be registered as a redirect URI with that IdP.

## Probe

The probe evaluates each entry: every role in the entry must be met.

* **Unprefixed** roles are checked against `session_users.roles`.
* **`ip:`** roles are checked against the client IP and the ranges of the IpRestriction provider each role points at.
* **`appointment:`** roles are checked against the session's appointments and the asset's fields.

The result:

* **200** if any entry is met.
* **401** if some entry could be met after a user action: sign in, enter a key, accept terms.
* **403** if none could: wrong location, signed in without the role, the appointment isn't approved yet, hasn't started or has ended, or the asset isn't in the appointment.

> [!TIP]
> The probe is evaluated even without a token, when there's an `ip:` role. An on-site user then gets 200 straight away, without any access service, token or session.

### Probe wording

The probe's `heading` and `note` explain the reason, e.g. "Your appointment starts at 10:00", or "Sign in with your University account, or enter your appointment key. It can also be viewed in the Research Centre."

This wording doesn't belong on `access_services`. It varies by *reason*, not by service, is built from parts of several entries, and often has no service to attach to (a 403, or an IP-only asset). So:

* Messages are **keyed templates with placeholders** (e.g. `blocked.appointmentNotStarted.note` with `{start}` and `{end}`), with defaults in code.
* A new `probe_messages` table holds a customer's overrides, optionally per role provider (e.g. one IdP's sign-in phrase). A message is taken from the provider's row if there is one, then the customer's row, then the default.
* Place names for `{where}` come from IpRestriction provider configuration (`placeName`).

## Appendix

### Suggested Data Model Changes

All in the IIIF-Auth-V2 database:

```sql
-- roles: access_service_id is replaced by role_provider_id, the provider OFFERED for the role
ALTER TABLE roles DROP COLUMN access_service_id;
ALTER TABLE roles ADD COLUMN role_provider_id uuid NOT NULL REFERENCES role_providers (id);

-- access_services: role_provider_id is replaced by a set of providers
ALTER TABLE access_services DROP COLUMN role_provider_id;

CREATE TABLE access_service_role_providers (
    access_service_id uuid NOT NULL REFERENCES access_services (id),
    role_provider_id  uuid NOT NULL REFERENCES role_providers (id),
    PRIMARY KEY (access_service_id, role_provider_id)
);

-- appointments (the shape used by the appointments app), and the appointments a session holds
CREATE TABLE appointments (
    id               uuid                     PRIMARY KEY,
    customer         integer                  NOT NULL,
    role_provider_id uuid                     NOT NULL REFERENCES role_providers (id),
    token            text                     NOT NULL,   -- the key the user enters
    roles            text[]                   NOT NULL,   -- appointment: roles it grants
    start            timestamp with time zone NOT NULL,
    "end"            timestamp with time zone NOT NULL,
    active           boolean                  NOT NULL,
    approved_by      text,
    date_approved    timestamp with time zone,
    name             text,
    email            text,
    notes            text,
    created          timestamp with time zone NOT NULL DEFAULT now(),
    asset_patterns   jsonb                    NOT NULL,   -- e.g. {"customer": [2], "scopes": ["b76hr45e"], "string1": ["LIDDLE/WW2/HFIU"]}
    UNIQUE (customer, token)
);

CREATE TABLE session_appointments (
    session_user_id uuid NOT NULL REFERENCES session_users (id),
    appointment_id  uuid NOT NULL REFERENCES appointments (id),
    PRIMARY KEY (session_user_id, appointment_id)
);

-- role_provision_tokens: which provider a combination service's sign-in is for, and the appointment
-- carried through the significant gesture
ALTER TABLE role_provision_tokens ADD COLUMN role_provider_id uuid REFERENCES role_providers (id);
ALTER TABLE role_provision_tokens ADD COLUMN appointment_id uuid REFERENCES appointments (id);

-- probe wording overrides
CREATE TABLE probe_messages (
    customer         integer NOT NULL,
    key              text    NOT NULL,
    role_provider_id uuid    REFERENCES role_providers (id),   -- null = customer-wide
    text             jsonb   NOT NULL                          -- LanguageMap
);
CREATE UNIQUE INDEX ix_probe_messages_customer_key_provider
    ON probe_messages (customer, key, COALESCE(role_provider_id, '00000000-0000-0000-0000-000000000000'::uuid));
```

`role_providers.configuration` gains the `IpRestriction` and `Appointment` types. `session_users` is unchanged.

Migrating existing data:

* `roles.role_provider_id` is the `role_provider_id` of the role's current `access_service_id`.
* Each existing access service gets one `access_service_role_providers` row for its current provider.
* Each customer with interactive providers needs a consolidated service offering all of them.

### Changes Required

#### IIIF-Auth-V2

* **Schema** as above, including migrations.
* **`/services/`** (`GetServicesDescription`): emission rules as above.
  * *(Suggestion)* A `GET /services/{customer}?entry=…` form that returns only the access services, so Orchestrator can cache it across assets (see [Caching services descriptions](#caching-services-descriptions)).
  * *(Suggestion)* Accept role entries as `POST` JSON on `/probe_internal/` and `/verifyAccess/` (see [Storing and transmitting role entries](#storing-and-transmitting-role-entries)).
* **`/probe_internal/` and `/verifyAccess/`**: evaluate role entries, using the session's roles, the client IP (`ip:`) and the session's appointments plus the asset's fields (`appointment:`). Evaluate without a token or cookie when there are `ip:` roles. Return 200, 401 or 403 as above, with per-reason wording.
* **Client IP**: read from `X-Forwarded-For`, trusted only from known proxies.
* **Sessions accumulate**: a new grant merges roles and appointments into the existing session instead of replacing it. (Known issue, already on the backlog.) Logout ends everything in the session.
* **Access service pages**:
  * resolve the provider from `access_service_role_providers`, not `AccessService.RoleProvider`
  * a choice page for services with several interactive providers, with a way to pick one (e.g. a `provider` parameter)
  * an appointment key form and `POST` handler (`AppointmentProviderHandler`)
* **OAuth callback**: take the provider from `role_provision_tokens.role_provider_id`.
* **`ClickThroughProviderHandler`**: grant roles by `role_provider_id`.
* **API**:
  * CRUD for role providers (the new types), roles (`role_provider_id`), access service provider sets, appointments and probe messages
  * validation of the rules above

#### Orchestrator

* **Probe** (`ProbeServiceSupport`): if there's no bearer token, still call `/probe_internal/` when any entry has an `ip:` role. Otherwise keep returning 401 locally.
* **Image and info.json requests** (`AssetAccessValidator`): if there's no Auth2 cookie, still call `/verifyAccess/` when an entry is made entirely of `ip:` roles. Otherwise keep returning 401 locally. Cache results per client IP and role set, as this adds a call per image request for anonymous users of location-restricted assets.
* **Propagate `X-Forwarded-For`** to IIIF-Auth-V2 when there's an `ip:` role. Today only `Cookie` is propagated.
* **Send the asset's metadata fields** (`space`, `scopes`, `tags`, `string1`–`3`, `number1`–`3`) to `/probe_internal/` and `/verifyAccess/` when there's an `appointment:` role. `OrchestrationAsset` doesn't carry these today, so the asset tracker needs to load and cache them.
* **Role expressions**: pass entries in the agreed encoding (suggested above: a JSON body for `/probe_internal/` and `/verifyAccess/`, repeated `entry` parameters for `/services/`).
* *(Suggestion)* **Cache `/services/` responses** by normalised URL and build the probe service locally (see [Caching services descriptions](#caching-services-descriptions)).

#### Protagonist API / Database

* **Asset roles become entries.** This changes their storage and validation (including rejecting inconsistent entries, see [Role entries](#role-entries-and--or)), and how they're passed to IIIF-Auth-V2.
* Role prefixes are part of the role URI, so they need no extra column.

## Open Decisions

There are suggested approaches for some of these above, they can be confirmed and documented in relevant tickets.

* **Role expression encoding**: how asset role entries are stored and passed between services. A suggestion, with alternatives, is in [Storing and transmitting role entries](#storing-and-transmitting-role-entries).
* **Emitting `external`**: proposed only when IP alone grants access. In spec order, `external` is listed first, which matters for clients that only use the first service. The alternative is never emitting `external`, and relying on the probe and token-less evaluation.
* **Probes rejected locally**: with no token and no `ip:` role, Orchestrator answers a generic "Missing credentials" without calling IIIF-Auth-V2, so the user isn't told what they'd need. Always calling `/probe_internal/` would explain it, at the cost of a call per anonymous probe.
* **Signed in, no roles**: an IdP sign-in whose claims map to no roles creates no session today, so the probe stays 401 ("sign in") rather than 403 ("your account doesn't have access"). A session with no roles would record it.
* **Content warnings for staff**: if Entra also grants a clickthrough role, staff skip that warning everywhere, including where an entry needs both. That's a per-role configuration choice.
* **Several appointments, shared keys**: `session_appointments` allows several appointments per session. Whether a key may be used in several sessions (i.e. shared) is a policy question.
* **Probe wording language**: `probe_messages.text` is a LanguageMap, so the probe needs to choose a language, e.g. from `Accept-Language`.
* **Appointment storage**: a table in IIIF-Auth-V2, or a lookup in an external booking system.
* **IP ranges granting other kinds of role**: not supported (see [IpRestriction providers grant only `ip:` roles](#IpRestriction-providers-grant-only-ip-roles)); asset entries cover the need. If it's ever required, an `alsoGrants` list could be added to the configuration, accepting that Orchestrator would then have to call IIIF-Auth-V2 for every protected asset.
* **One pattern object per appointment**: as fields combine with AND, one appointment can't grant two unrelated things identified by *different* fields (e.g. a Manifest scope *or* an archival series). If that's needed, `asset_patterns` could become an array of pattern objects, any of which matches.
