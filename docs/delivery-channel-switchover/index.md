Starting condition

deliveryChannels   =>  (in practice, nulls) Image.IOP, Image.ThumbnailPolicy


Step 1 - https://github.com/dlcs/protagonist/pull/707

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

Step 2 - Deploy this Protagonist

Step 3 - https://github.com/dlcs/protagonist/issues/615

In Wellcome DDS, replace all C# properties and other usages of `DeliveryChannels` with `WcDeliveryChannels`, and use `.wcDeliveryChannels` on all DLCS API calls.

Step 4 - Deploy this DDS

Step 5 - 

Replace the `string[] DeliveryChannels` property on the Hydra Image API class in the DLCS with `DeliveryChannel[] DeliveryChannels` as per the new documentation.

Refactor DLCS throughout to use `OldDeliveryChannels` property from Hydra API for old delivery channel behaviour:

```
    [JsonProperty(PropertyName = "deliveryChannels")]
    public DeliveryChannel[]? DeliveryChannels { get; set; }

    [JsonProperty(PropertyName = "wcDeliveryChannels")]
    public string[]? OldDeliveryChannels { get; set; }
    
    // removed:  public string[]? WcDeliveryChannels { ... }
```

Step 6 - Deploy this Protagonist

(Wellcome carries on fine while:)

Steps 7..n

Implement new DeliveryChannel behaviour in DLCS in `protagonist:develop` branch, using the DeliveryChannels
The old delivery channel behaviour is kept, engine uses it etc throughout this work

Rewrite DDS against new DLCS DeliveryChannel API in `iiif-builder:develop` branch

QUESTION - are we able to support both old and new models? Does that make it easier or harder?

Step n+1 - Set up all the customer 0 default policies, Wellcome Customer 2 copies of those policies
Make a `../video-default` DeliveryChannelPolicy with the policyData "video-mp4-720p"
Make a `../audio-default` DeliveryChannelPolicy with the policyData "audio-mp3-128"
Make a image policy for JP2s
Make a thumb policy of `["!1024,1024", "!400,400", "!200,200", "!100,100"]`
Make a file policy
Set up all the defaults

Demonstrate that this all works as expected for use of the NEW `"deliveryChannels": [...]` => `DeliveryChannels` API

Step n+2

At the DLCS API level, convert incoming `OldDeliveryChannels`, `ImageOptimisationPolicy` and `ThumbnailPolicy` setter calls (from Wellcome) into equivalent new Delivery Channel resources, and convert the getters. 
This means we are no longer using the `OldDeliveryChannels` internally; strip all that code out.
API, Engine, Orchestrator all use the full new DeliveryChannels as per docs, but the API will translate the incming settings from Wellcome into their new equivalents, and emulate the old properties (ish)
(This can't be just simple getter and setter interception as there are three fields that need to be evaluated together; has to happen on persistence.)

I think all normal IOP and Thumbpolicy on assets is going to be `null` anyway so this shouldn't be too complicated.
