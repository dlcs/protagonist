## Starting condition - where we are today

Only Wellcome uses `deliveryChannels`, using the 2023 interim implementation.

The `deliveryChannels` property takes an array of strings, e.g., `["iiif-img","file"]`.

It doesn't take `"thumbs"`, we left separate handling of that to this new work.

It continues to use `ImageOptimisationPolicy` (IOP) and `ThumbnailPolicy`. For jp2s it will pass `"use-original"`.
For current stuff it will pass `null` for ThumbnailPolicy.

For AV it will always pass `null` for ImageOptimisationPolicy, and Engine will use the defaults (video-max).

---

## Background work

 - [The dlcs stage cleanup function needs to be updated to cleanup delivery channels](https://github.com/dlcs/protagonist/issues/708)
 - [Write Playwright tests for Delivery Channel API documentation](https://github.com/dlcs/protagonist/issues/703)

## Step 1 - handle wcDeliveryChannels

 - [Rename Hydra API deliveryChannels property in 2023 Protagonist to wcDeliveryChannels](https://github.com/dlcs/protagonist/issues/614)

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

 - [Reimplement Wellcome DDS against wcDeliveryChannels Property](https://github.com/dlcs/protagonist/issues/615)

In Wellcome DDS, replace all C# properties and other usages of `DeliveryChannels` with `WcDeliveryChannels`, and use `.wcDeliveryChannels` on all DLCS API calls.


## Step 4 - Deploy this DDS

It's as if `.deliveryChannels` had never existed. But otherwise, everything else is the same, still no functional changes in DDS client, or Protagonist server.



## Step 5 - Thumb generation spike


 - [SPIKE for #256 - can we run thumb-making image server as an Engine sidecar?](https://github.com/dlcs/protagonist/issues/658)



## Step 6 - Replace DeliveryChannels property with new version   

- [Replace string[] DeliveryChannels with DeliveryChannel[] DeliveryChannels](https://github.com/dlcs/protagonist/issues/717)
  
Replace the `string[] DeliveryChannels` property on the Hydra Image API class in the DLCS with `DeliveryChannel[] DeliveryChannels` as per the new documentation. Nothing will be calling this any more. All old calls still get routed to `OldDeliveryChannels` via `.wcDeliveryChannels` on hydra JSON.

Leave that property intact on the Hydra class, but ALL use of it within the DLCS is now for removal, as the new behaviour is implemented. We'll come back to deal with this property in a later step (emulation).

We also, during the process, need to remove all usage of ImageOptimisationPolicy and ThumbnailPolicy.

We don't necessarily have to have a specific task to rip it all out before introducing the new DeliveryChannels, might be better to replace bit by bit, so we can see what the old code is doing in places as we implement the new. But the old code will never more be executed.

```
    [JsonProperty(PropertyName = "deliveryChannels")]
    public DeliveryChannel[]? DeliveryChannels { get; set; }

    [JsonProperty(PropertyName = "wcDeliveryChannels")]
    public string[]? OldDeliveryChannels { get; set; }
    
    // removed:  public string[]? WcDeliveryChannels { ... }
```

(we cannot deploy `develop` protagonist for a while now, because `wcDeliveryChannels`, `ImageOptimisationPolicy` and `ThumbnailPolicy` are stubs that don't get used _for now_).


## Steps 6..n - Implement new DeliveryChannels in Protagonist

Implement new DeliveryChannel behaviour in DLCS in `protagonist:develop` branch, using the DeliveryChannels documentation.

That is, make `deliveryChannels` work as described there, and the rest of the new API and resources.
These resources and features are all independent of the old behaviour.



 - [Modify DB Schema: add tables for delivery channels](https://github.com/dlcs/protagonist/issues/618) 
 - [Create "global" DeliveryChannelPolicies and DefaultDeliveryChannels](https://github.com/dlcs/protagonist/issues/619)
 - [Customer Creation allocates correct DeliveryChannelPolicies and DefaultDeliveryChannels](https://github.com/dlcs/protagonist/issues/716)
 - [Implement default deliveryChannels and retire DLCS:IngestDefaults appSetting](https://github.com/dlcs/protagonist/issues/625)

 - [API endpoints to manage default delivery channels and delivery channel policies](https://github.com/dlcs/protagonist/issues/634)
  
 - [Update API to receive and emit full DeliveryChannel information](https://github.com/dlcs/protagonist/issues/624)
 
 - [Update logic for determining if an Asset has a particular deliveryChannel](https://github.com/dlcs/protagonist/issues/621)
 - [Update Engine HydrateAssetPolicies() method for delivery channel model](https://github.com/dlcs/protagonist/issues/622)

 - [Using a sidecar Cantaloupe for thumbs delivery channel ingest](https://github.com/dlcs/protagonist/issues/256)
 
 - [How do iiif-av policies become Elastic Transcoder settings?](https://github.com/dlcs/protagonist/issues/709)
 - (includes actually doing it)

 - [Cleanup Handler use of Delivery Channels](https://github.com/dlcs/protagonist/issues/691)
 
 - [Ensure s.json thumb sizes are emitted in sizes from named query manifests and single-asset manifests](https://github.com/dlcs/protagonist/issues/631)

 - [Complete handling of thumbs delivery channel](https://github.com/dlcs/protagonist/issues/629)


 - [Create "unofficial" thumbs even when no thumbs channel specified](https://github.com/dlcs/protagonist/issues/627)
 - [Investigate issues with CMYK JPEGs, incompatible gifs and other Appetiser issues](https://github.com/dlcs/protagonist/issues/684)

### QUESTION

> Are we able to support both old and new models? Maintain existing behaviour for wcDeliveryChannels, IOP and ThumbPolicy alongside the emerging new work?

Yes - just emulating at the API layer (see #714)


 - [(Create DB Migration) Convert existing ‘IOP’,‘ThumbnailPolicy’ and ‘DeliveryChannel’ values from Images table into ImageDeliveryChannels table](https://github.com/dlcs/protagonist/issues/620) ?







## Step xx - Implement "emulation" - Convert `.wcDeliveryChannels` on incoming hydra to new model

 - [Emulation layer to convert `.wcDeliveryChannels` at the API level](https://github.com/dlcs/protagonist/issues/714)



## Step xx+1 - Deploy this Protagonist

- DB migration of Wellcome prod DB
- (Wellcome prod has new tables)
- Wellcome default DCs and DeliveryChannelPolicy tables are populated
- The 100m+ ImageDeliveryChannel rows are **populated** 


Wellcome, the only user of `wcDeliveryChannels`, carries on fine, no change in behaviour.
DLCS will be converting Wellcome's old usage into new model




## Step n+1 - Run Wellcome `develop` on DDS stage against Protagonist calling new deliveryChannels 

This should be demonstrated to produce the same results as the "emulated" version.



## Step n+2 - Deploy DDS develop (appendix A) to production


## Step n+3

 - [Make sure Wellcome has correct DeliveryChannelPolicies and DefaultDeliveryChannels](https://github.com/dlcs/protagonist/issues/718)
 - [Create new "work-page-friendly" thumbnail policy for Wellcome and reingest images](https://github.com/dlcs/protagonist/issues/635)

## Step n+4 - Remove "emulation" and old properties

 - [Drop IOP and ThumbnailPolicy columns from Asset](https://github.com/dlcs/protagonist/issues/623)
 - Drop OldDeliveryChannels (WcDeliveryChannels)
  
   
 - [Delete no-longer required delivery artifacts](https://github.com/dlcs/protagonist/issues/430)








## Appendix A - Rewrite Wellcome against the new API

 - [Rewrite DDS (iiif-builder) delivery channel use against prototype documentation](https://github.com/dlcs/protagonist/issues/617)
 - [Update Wellcome iiif-builder to store thumb sizes differently in Manifestations table](https://github.com/dlcs/protagonist/issues/633)
 - [Update Wellcome iiif-builder to read image sizes from DLCS](https://github.com/dlcs/protagonist/issues/632)

This can't yet be tested. New code at Wellcome uses the `deliveryChannels` property on the API as if it were a brand new feature.
Rewrite DDS against new DLCS DeliveryChannel API in `iiif-builder:develop` branch.

It helps to do this ahead of the next step in case it uncovers any issues with the proposed implementation.


