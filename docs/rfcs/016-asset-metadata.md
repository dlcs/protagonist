# Storing Asset Metadata

## Executive Summary

Create a new `AssetApplicationMetadata` table to store metadata about an Asset for internal use only. This will have a flexible schema to be able to store whatever shape of data is required for an asset.

## Motivation

Ticket [#631](https://github.com/dlcs/protagonist/issues/631) introduces the need to read available image thumbnail sizes at scale. 

Currently 'at scale' operations (e.g. projecting NamedQuery to IIIF Manifest) are done by calculating the available sizes per image on the fly. This is done by using the width + height of the image and relevant thumbnail policy. 

However, now that thumbnail sizes are defined as [IIIF ImageApi Size parameters](https://iiif.io/api/image/3.0/#42-size) this is no longer a viable option as it would involve fairly complex size calculations and could fall foul of off-by-one rounding issues, as seen in the past. To negate this we now use an ImageServer to generate the thumbnails and store the actual sizes of those thumbnails (see [ADR 0006 - Engine ImageServer](../adr/0006-engine-imageserver.md)).

The generated thumbnail sizes are stored in `s.json`, a json file in S3 (see [RFC 001 - Thumbs](001-thumbnails.md)). This is quick to read and parse when handling single asset requests but it would be very inefficient to do so at scale. 

This RFC suggests at an alternative approach to storing the thumbnail sizes for an image.

## Proposed Implementation

The proposal is to store the generated thumbnail sizes in the database, in a separate table from `Images`. Proposed name for this table is `AssetApplicationMetadata` - a table designed to store metadata about an Asset for internal use by the application only; the values would never be expose via API.

This table will initially store the available thumbnail sizes, duplicating what is stored in `s.json`, but can easily be read as part of a database query. These can be read in bulk alongside the corresponding `Asset` record. We should continue to write `s.json` to S3 as it allows thumb-serving to remain self-contained, without a need to read database to handle requests.

The handling of a NamedQuery is fairly complex to allow for query building reuse. Currently reading metadata is only required for manifest projection so we will need to add a hook in the processing to add the required `.Include()` where appropriate.

### Future Improvements

While we are only storing thumbnailSizes now, this new table could be used to store a variety of values in the future. Some examples are:

* Generated transcode types and output locations for AV.
* For `file` delivery channel - do we store a copy of the original file? If so, where.
* For images - do we store a copy of the file? Is it original (`use-original`) or a transcode?
* Adjuncts: what is stored where?
* Checksum of Asset origin - could help to identify when source image has been updated.
* Periodic request metrics. An external request could calculate metrics and periodically write summary back to db (per day/month/year). 

The above values can then be used to drive generation of improved [single-item manifest](https://github.com/dlcs/protagonist/issues/488) and clearing up [no-longer required delivery artifacts](https://github.com/dlcs/protagonist/issues/430).

### Database

#### Table Name

Considered names and reason for choosing or not:
* `AssetApplicationMetadata` - chosen name as doesn't add restriction to what is being stored but `Application` in name highlights that this is for internal metadata only, not replacements for string1, string2 etc.
* `ImageMetadata` - while suitable this is vague and opens up the table to be a dumping ground for any data.
* `DeliveryChannelMetadata` - originally considered name, this would point to having both an `assetId` and `channel` record per row but some values may be relevant for multiple channels (namely image + thumbs). We may also want to store some data that is not delivery-channel specified (e.g. checksum).
  * If we opted for this table we could consider adding a new column to the [`ImageDeliveryChannel`](014-delivery-channels-database.md) table, as this would store a row per Asset/Delivery channel.
* `AssetDeliveryMetadata` - similar to above - if we are storing checksum etc this isn't asset delivery (ie Orchestrator/Thumbs) specific.

#### Schema

The suggested schema for the table should be flexible.

| Column        | Type        | Description                         |
| ------------- | ----------- | ----------------------------------- |
| AssetId       | text        | AssetId this is for                 |
| MetadataType  | text        | Identifier for the type of metadata |
| MetadataValue | jsonb       | JSON object of values for type      |
| {audit-cols}  | timestamptz | Created/updated dates               |

* `AssetId` - this maintains link back to asset. Storing Id only is fine, no need to store separate `customer` or `space` as lookup will only be by Id.
* `MetadataType` is the 'key' used to look up relevant metadata - these values won't link to anything in the database but a known list of values will be maintained and used by the application code.
* `MetadataValue` is a `jsonb` value storing relevant data as JSON. In most cases I think we would always want to read the entire object but it could be useful to have efficient querying afforded by `jsonb` (e.g. to get `"o"`pen thumbs only). This querying has support in [npgsql](https://www.npgsql.org/efcore/mapping/json.html).
* `AssetId` and `MetadataType` would be composite key.

#### Sample Data

Example data for an asset could be:

| AssetId | MetadataType | MetadataValue                                                     |
| ------- | ------------ | ----------------------------------------------------------------- |
| 1/2/foo | ThumbSizes   | `{"o": [[200,127],[100,64]],"a": [[1024,651], [400,254]]}`        |
| 1/2/foo | AVTranscodes | `["1/2/foo/full/max/default.mp3","1/2/foo/full/max/default.avi"]` |
| 1/2/foo | Checksum     | `{"sha256": "abc123123123"}`                                      |

`ThumbSizes` is the only type we're interested in now but the other values are indicative of what we could store.

#### Querying / EF Entities

Objects could be included where required via filtered include statement to filter on `MetadataType`. 

```cs
var assetWithThumbs = dbContext.Images
                .Include(i => i.AssetApplicationMetadata.Where(m => m.MetadataType == "ThumbSizes"))
                .Single(i => i.Id == assetId);
```