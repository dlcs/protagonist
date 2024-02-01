-- convert image defaults
INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT images."Id", 'iiif-img', (SELECT "DeliveryChannelPolicies"."Id" from "DeliveryChannelPolicies" WHERE ("DeliveryChannelPolicies"."Customer", "Channel", "DeliveryChannelPolicies"."Name") = (1, 'iiif-img', 'default'))
    FROM (SELECT * FROM  "Images" WHERE  "Images"."DeliveryChannels" LIKE '%iiif-img%' AND "Images"."ImageOptimisationPolicy" <> 'use-original' AND "NotForDelivery" = false) AS images
UNION
    SELECT "Images"."Id", 'thumbs', DCP."Id" FROM  "Images"
        JOIN public."DeliveryChannelPolicies" DCP on "Images"."Customer" = DCP."Customer"
        WHERE  "Images"."DeliveryChannels" LIKE '%iiif-img%' AND "Images"."ImageOptimisationPolicy" <> 'use-original' AND "Channel" = 'thumbs' AND DCP."Name" = "Images"."ThumbnailPolicy" AND "NotForDelivery" = false;

-- convert image use original
INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT images."Id", 'iiif-img', (SELECT "DeliveryChannelPolicies"."Id" from "DeliveryChannelPolicies" WHERE ("DeliveryChannelPolicies"."Customer", "Channel", "DeliveryChannelPolicies"."Name") = (1, 'iiif-img', 'use-original'))
    FROM (SELECT * FROM  "Images" WHERE  "Images"."DeliveryChannels" LIKE '%iiif-img%' AND "Images"."ImageOptimisationPolicy" = 'use-original' AND "NotForDelivery" = false) AS images
UNION
    SELECT "Images"."Id", 'thumbs', DCP."Id" FROM "Images"
    JOIN public."DeliveryChannelPolicies" DCP on "Images"."Customer" = DCP."Customer"
    WHERE  "Images"."DeliveryChannels" LIKE '%iiif-img%' AND "Images"."ImageOptimisationPolicy" = 'use-original' AND "Channel" = 'thumbs' AND DCP."Name" = "Images"."ThumbnailPolicy" AND "NotForDelivery" = false;

-- convert audio

INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT images.ImageId, 'iiif-av', images.DeliveryChannelPolicyId
FROM (SELECT "Images"."Id" AS ImageId, DCP."Id" AS DeliveryChannelPolicyId FROM  "Images"
        JOIN public."DeliveryChannelPolicies" DCP on "Images"."Customer" = DCP."Customer"
        WHERE  "Images"."DeliveryChannels" LIKE '%iiif-av%' AND "Images"."MediaType" LIKE 'audio/%' AND "Channel" = 'iiif-av' AND DCP."Name" = 'default-audio' AND "NotForDelivery" = false) as images;

-- convert video

INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT images.ImageId, 'iiif-av', images.DeliveryChannelPolicyId
FROM (SELECT "Images"."Id" AS ImageId, DCP."Id" AS DeliveryChannelPolicyId FROM  "Images"
        JOIN public."DeliveryChannelPolicies" DCP on "Images"."Customer" = DCP."Customer"
        WHERE  "Images"."DeliveryChannels" LIKE '%iiif-av%' AND "Images"."MediaType" LIKE 'video/%' AND "Channel" = 'iiif-av' AND DCP."Name" = 'default-video' AND "NotForDelivery" = false) as images;

-- convert pdf file

INSERT INTO "ImageDeliveryChannels" ("ImageId", "Channel", "DeliveryChannelPolicyId")
SELECT images."Id", 'file', DCPImages."Id"
FROM (SELECT * FROM  "Images" WHERE  "Images"."DeliveryChannels" LIKE '%file%' AND "Images"."MediaType" = 'application/pdf' ANd "NotForDelivery" = false) AS images,
     (SELECT "DeliveryChannelPolicies"."Id" from "DeliveryChannelPolicies" WHERE ("DeliveryChannelPolicies"."Customer", "Channel", "DeliveryChannelPolicies"."Name") = (1, 'file', 'none')) AS DCPImages;


-- gets assets that don't have any delivery channel policies attached
SELECT * FROM "Images"
LEFT JOIN public."ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
WHERE IDC."Id" IS null;

SELECT "Images"."Id", "ThumbnailPolicy", "ImageOptimisationPolicy","DeliveryChannelPolicyId", DCP."Name" AS DeliveryChannelPolicyName, DCP."Channel" FROM "Images"
LEFT JOIN public."ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
JOIN public."DeliveryChannelPolicies" DCP on DCP."Id" = IDC."DeliveryChannelPolicyId"
WHERE IDC."Id" IS NOT null;