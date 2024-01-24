## Starting condition - where we are today

Only Wellcome uses `deliveryChannels`, using the 2023 interim implementation.

The `deliveryChannels` property takes an array of strings, e.g., `["iiif-img","file"]`.

It doesn't take `"thumbs"`, we left separate handling of that to this new work.

It continues to use `ImageOptimisationPolicy` (IOP) and `ThumbnailPolicy`. For jp2s it will pass `"use-original"`.
For current stuff it will pass `null` for ThumbnailPolicy.

For AV it will always pass `null` for ImageOptimisationPolicy, and Engine will use the defaults (video-max).

---


## Step 1 - handle wcDeliveryChannels

https://github.com/dlcs/protagonist/pull/707

`Image` hydra object has `DeliveryChannels`, `WcDeliveryChannels` and `OldDeliveryChannels`, all `string[]`

`.wcDeliveryChannels` on API gets and sets `Image.DeliveryChannels` (string[])
`.deliveryChannels`   on API gets and sets `Image.DeliveryChannels` (string[]) (via OldDeliveryChannels property)

To Wellcome API consumer, this is indistinguishable from the previous behaviour.

```
    [JsonIgnore]
    public string[]? DeliveryChannels { get; set; }

    [JsonProperty(PropertyName = "deliveryChannels")]
    public string[]? OldDeliveryChannels { set => DeliveryChannels = value; get => DeliveryChannels; }
    
    [JsonProperty(PropertyName = "wcDeliveryChannels")]
    public string[]? WcDeliveryChannels { set => DeliveryChannels = value; get => DeliveryChannels; }
```

## Step 2 - Deploy this Protagonist

Wellcome DDS as a client doesn't see any difference.


## Step 3 - Reimplement DDS against `wcDeliveryChannels`

https://github.com/dlcs/protagonist/issues/615

In Wellcome DDS, replace all C# properties and other usages of `DeliveryChannels` with `WcDeliveryChannels`, and use `.wcDeliveryChannels` on all DLCS API calls.


## Step 4 - Deploy this DDS

It's as if `.deliveryChannels` had never existed. But otherwise, everything else is the same, still no functional changes in DDS client, or Protagonist server.


## Step 5 - Separate old and new functionality 

Refactor DLCS throughout (API, Engine, Orchestrator, Portal) to use `OldDeliveryChannels` property from Hydra API for old delivery channel behaviour.

Replace the `string[] DeliveryChannels` property on the Hydra Image API class in the DLCS with `DeliveryChannel[] DeliveryChannels` as per the new documentation. Nothing will be calling this any more. All old calls still get routed to `OldDeliveryChannels` and protagonist processes them as before.


```
    [JsonProperty(PropertyName = "deliveryChannels")]
    public DeliveryChannel[]? DeliveryChannels { get; set; }

    [JsonProperty(PropertyName = "wcDeliveryChannels")]
    public string[]? OldDeliveryChannels { get; set; }
    
    // removed:  public string[]? WcDeliveryChannels { ... }
```

## Step 6 - Deploy this Protagonist

Wellcome, the only user of `wcDeliveryChannels`, carries on fine, no change in behaviour.


## Step 7 - Rewrite Wellcome against the new API

https://github.com/dlcs/protagonist/issues/617

This can't yet be tested. New code at Wellcome uses the `deliveryChannels` property on the API as if it were a brand new feature.
Rewrite DDS against new DLCS DeliveryChannel API in `iiif-builder:develop` branch.

It helps to do this ahead of the next step in case it uncovers any issues with the proposed implementation.


## Steps 8..n - Implement new DeliveryChannels in Protagonist

Implement new DeliveryChannel behaviour in DLCS in `protagonist:develop` branch, using the DeliveryChannels documentation.

That is, make `deliveryChannels` work as described there, and the rest of the new API and resources.
These resources and features are all independent of the old behaviour.

Set up all the customer 0 default policies, Wellcome Customer 2 copies of those policies
Make a `../video-default` DeliveryChannelPolicy with the policyData "video-mp4-720p"
Make a `../audio-default` DeliveryChannelPolicy with the policyData "audio-mp3-128"
Make a image policy for JP2s
Make a thumb policy of `["!1024,1024", "!400,400", "!200,200", "!100,100"]`
Make a file policy
Set up all the defaults

### QUESTION

> Are we able to support both old and new models? Maintain existing behaviour for wcDeliveryChannels, IOP and ThumbPolicy alongside the emerging new work?

Does that make it easier or harder?

The old 2023 delivery channel behaviour is kept, Engine uses it throughout this work.

At the DLCS API level, convert incoming `OldDeliveryChannels`, `ImageOptimisationPolicy` and `ThumbnailPolicy` setter calls (from Wellcome) into equivalent new Delivery Channel resources, and convert the getters. 
This means we are no longer using the `OldDeliveryChannels` internally; strip all that code out.

What is the relationship of this task to [Convert existing ‘IOP’,‘ThumbnailPolicy’ and ‘DeliveryChannel’ values from Images table into ImageDeliveryChannels table](https://github.com/dlcs/protagonist/issues/620) ?

API, Engine, Orchestrator all use the full **new DeliveryChannels as per docs**, but the API will translate the incoming old property settings from Wellcome into their new equivalents, and emulate the old properties. **Is that actually possible?**

_This can't be just simple getter and setter interception as there are three fields that need to be evaluated together; has to happen on persistence._

However, apart from `use-original` all normal IOP and ThumbnailPolicy on assets is going to be `null` anyway, and we could reject anything that isn't for a short time.


## Step n+1 - Run Wellcome `develop` on DDS stage against Protagonist calling new deliveryChannels 

This should be demonstrated to produce the same results as the "emulated" version.


## Step n+2 - Deploy DDS develop to production


## Step n+3 - Remove "emulation" and old properties


