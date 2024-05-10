-- Note that for large datasets is may be performant to add an index for DeliveryChannels
-- create index IX_Image_DeliveryChannels on "Images" ("DeliveryChannels", "Family", "NotForDelivery");

BEGIN TRANSACTION;

-- convert image defaults
INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT "Id",
       'iiif-img',
       CASE
           WHEN "ImageOptimisationPolicy" != 'use-original' THEN 1 -- 1 is 'default' for image
           WHEN "ImageOptimisationPolicy" = 'use-original' THEN 2 -- 2 is 'use-original' for image
           END
FROM "Images"
WHERE "Family" = 'I'
  AND "DeliveryChannels" LIKE '%iiif-img%'
  AND "NotForDelivery" = false;

-- create thumbs - 1 per image
-- NOTE: For particularly large, single-customer deployments it will be quicker to use query similar to above and hardcode based on ThumbnailPolicy
INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT i."Id",
       'thumbs',
       dcp."Id"
FROM "Images" i
         INNER JOIN (SELECT p."Id", p."Name", p."Customer"
                     FROM "DeliveryChannelPolicies" p
                     WHERE "Channel" = 'thumbs') dcp
                    ON (i."Customer" = dcp."Customer" AND i."ThumbnailPolicy" = dcp."Name")
WHERE i."Family" = 'I'
  AND i."DeliveryChannels" LIKE '%iiif-img%'
  AND i."NotForDelivery" = false;

-- convert timebased
SELECT i."Id",
       'iiif-av',
       CASE
           WHEN "MediaType" LIKE 'audio/%' THEN (SELECT "Id"
                                                 FROM "DeliveryChannelPolicies"
                                                 WHERE "Customer" = i."Customer"
                                                   AND "Channel" = 'iiif-av'
                                                   AND "Name" = 'default-audio')
           WHEN "MediaType" LIKE 'video/%' THEN (SELECT "Id"
                                                 FROM "DeliveryChannelPolicies"
                                                 WHERE "Customer" = i."Customer"
                                                   AND "Channel" = 'iiif-av'
                                                   AND "Name" = 'default-video')
           END
FROM "Images" i
WHERE i."Family" = 'T'
  AND i."DeliveryChannels" LIKE '%iiif-av%'
  AND i."NotForDelivery" = false;

-- convert file
INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT "Id",
       'file',
       4 -- 4 is the "file" channel for system
FROM "Images"
WHERE "DeliveryChannels" LIKE '%file%'
  AND "NotForDelivery" = false;

COMMIT;

-- drop index IX_Image_DeliveryChannels;