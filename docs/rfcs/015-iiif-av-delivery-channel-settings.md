# How are iiif-av settings going to be maintained in protagonist


## Problem

https://deploy-preview-2--dlcs-docs.netlify.app/api-doc/delivery-channels#iiif-av

We decided to use named _DLCS_ presets as the policyData.

```
   "policyData": "[ \"video-mp4-720p\" ]",
```

When Engine is given an asset with:

```json
  "origin":  "s3://my-bucket/video-masters/huge-video.mpeg",
  "deliveryChannels": [
    {
      "@type": "vocab:DeliveryChannel",
      "channel": "iiif-av",
      "policy": "https://api.dlcs.io/customers/2/deliveryChannelPolicies/iiif-av/standard"
    }
  ]
```

... and `https://api.dlcs.io/customers/2/deliveryChannelPolicies/iiif-av/standard` looks like this:

```json
{
     "@id": "https://api.dlcs.io/customers/2/deliveryChannelPolicies/iiif-av/standard",
     "@type": "vocab:DeliveryChannelPolicy",
     "displayName": "Default video transcode",
     "channel": "iiif-av",
     "policyData":  "[ \"video-mp4-720p\" ]",
     "policyModified": "2023-09-19T15:36:58.6023600Z"
 }
```

...then Engine _looks up_ "video-mp4-720p" and sees, in some form, `"Transcode with "System preset: 'Mp4 HLS 720p'; Extension: 'mp4'"`, so it calls Elastic Transcoder with that information.

This RFC is to discuss where this data is stored within protagonist

## Legacy Implementation

Full details found [here](https://github.com/dlcs/protagonist/issues/709) and summarized here:

AV policies are a comma separated list found in the `ImageOptimisationPolcies` table that can consist of either elastic transcoder standard presets and friendly names that link to values in appsettings.

The system presets can be seen by using the aws command `aws elastictranscoder list-presets --query="Presets[].Name"` and this comes back like this:

```json
[
    "System preset: Generic 1080p",
    "System preset: Generic 720p",
    "System preset: Generic 480p 16:9",
    "System preset: Generic 480p 4:3",
    "System preset: Generic 360p 16:9",
    "System preset: Generic 360p 4:3",
    "System preset: Generic 320x240",
    "System preset: iPhone4S",
    "System preset: iPod Touch",
    "System preset: Apple TV 2G",
    "System preset: Apple TV 3G",
    "System preset: Web",
    "System preset: KindleFireHD",
    "System preset: KindleFireHD8.9",
    "System preset: Audio AAC - 256k",
    "System preset: Audio AAC - 160k",
    "System preset: Audio AAC - 128k",
    "System preset: Audio AAC - 64k",
    "System preset: KindleFireHDX",
    "System preset: NTSC - MPG",
    "System preset: PAL - MPG",
    "System preset: Full HD 1080i60 - MP4",
    "System preset: Full HD 1080i50 - MP4",
    "System preset: Gif (Animated)",
    "System preset: Web: Flash Video",
    "System preset: Full HD 1080i60 - XDCAM422",
    "System preset: Full HD 1080i50 - XDCAM422",
    "System preset: Webm 720p",
    "System preset: Webm VP9 720p",
]
```

_NOTE_: not the full list

For the friendly names, these are essentially system presets, linked to a more friendly name in the appsettings file, under TranscoderMappings like this:

``` json
  "TimebasedIngest": {
    "PipelineName": "dlcsspinup-timebased",
    "TranscoderMappings": {
      "Wellcome Standard MP4": "System preset: Web"
    }
  },
```

From this, the preset is used to kick off an ET `job` and then get pushed into buckets for output

## Delivery Channel Proposal

Firstly, as [discussed](https://github.com/dlcs/protagonist/issues/709), The `policyData` is an array of policies *which each link to a single system preset*, as opposed to combined values having multiple outputs.  These values are then looped through to create all the outputs that are required.  From an orchestrator perspective, nothing will have changed as these transcoder outputs will not change.

Due to the above, the likely best place to put these values is a dictionary in appsettings, similar to how `TranscoderMappings` works.  This is because there are a limited number of presets we want to support (dev currently uses only 3), rather than all of them and that it is unlikely for these values to need to be changed regularly.  Additionally, `TranscoderMappings` should be deprecated, with a new setting created called `DeliveryChannelMappings`, which consists of key value pairs like the below:

```json
  "TimebasedIngest": {
    "PipelineName": "dlcsspinup-timebased",
    "TranscoderMappings": { // deprecated
      "Wellcome Standard MP4": "System preset: Web"
    }, 
    "DeliveryChannelMappings": {
      "video-mp4-480p": "System preset: Generic 480p 16:9",
      "video-webm-720p": "System preset: Webm 720p(webm)",
      "audio-mp3-128k": "System preset: Audio MP3 - 128k(mp3)"
    }
  },
```

If this becomes unwieldy in the future, it may be worth moving to a table in the database, but currently given the limited number of entries, a dictionary in appsettings works for the moment.

Deprecated code can be stripped out at the point `oldDeliveryChannels` is removed.

While the names of these policies can technically be anything, the following convention will be used to simplify understanding:

```
<channel>-<format>-<quality>
```
_NOTE:_ the value should also be lowercase

Finally, this format can be extended with additional information, for example `System preset: Generic 480p 4:3`, could become `video-mp4-480p-4:3` if there was already a `System preset: Generic 480p 16:9` used.

## Location of settings

These settings will be located in the engine as a single source of truth.

For the API, an API call in engine will be made available to the API to retrieve the valid values.  These should be retrieved at the point the policies are required (i.e.: uploading media), rather than on startup and should be cached for a period of time.
