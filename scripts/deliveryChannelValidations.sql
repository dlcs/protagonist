-- general selects

SELECT count(*) FROM "Images";

SELECT count(*) FROM "ImageDeliveryChannels";


-- iif-img/thumbs selects - should provide numbers close to each other, but might not be the exact same

SELECT count(*) FROM "Images" where "MediaType" LIKE 'image/%';

SELECT count(*) FROM "ImageDeliveryChannels" where "Channel" = 'iiif-img';

SELECT count(*) FROM "ImageDeliveryChannels" where "Channel" = 'thumbs';

-- checking counts for any image that isn't use original

SELECT count(distinct("Id")) FROM "Images" where "ImageOptimisationPolicy" <>  'use-original' AND "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False;
SELECT count(distinct("ImageId")) FROM "ImageDeliveryChannels"
    JOIN "DeliveryChannelPolicies" DCP on DCP."Id" = "ImageDeliveryChannels"."DeliveryChannelPolicyId"
                where "ImageDeliveryChannels"."Channel" = 'iiif-img'
                AND DCP."Name" <> 'use-original';

-- retrieves id of any image that doesn't have a related image delivery channel

SELECT "Id" FROM "Images" where "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels"
    JOIN "DeliveryChannelPolicies" DCP on DCP."Id" = "ImageDeliveryChannels"."DeliveryChannelPolicyId"
                where "ImageDeliveryChannels"."Channel" = 'iiif-img';

-- retrieves images that should have a default policy

SELECT * FROM "Images"
            JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
            JOIN "DeliveryChannelPolicies" DCP on DCP."Id" = IDC."DeliveryChannelPolicyId"
            where "ImageOptimisationPolicy" <>  'use-original' AND "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False AND
                  IDC."Channel" = 'iiif-img'
                AND DCP."Name" = 'default';

-- retrieves image delivery channels for images that don't have iiif-img or thumbs - this will essentially be anything that has the file channel

SELECT * FROM "Images"
         JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
         where "MediaType" LIKE 'image/%' AND IDC."Channel" <>'iiif-img' AND IDC."Channel" <> 'thumbs' ;

-- retrieves images without the use-original policy, that have been created before scripts should have been run

SELECT "ImageId", I."Created", DCP."Name" FROM "ImageDeliveryChannels"
    JOIN "DeliveryChannelPolicies" DCP on DCP."Id" = "ImageDeliveryChannels"."DeliveryChannelPolicyId"
    JOIN "Images" I on I."Id" = "ImageDeliveryChannels"."ImageId"
                where "ImageDeliveryChannels"."Channel" = 'iiif-img'
                AND DCP."Name" <> 'use-original' AND I."Created" < CURRENT_DATE - 2
EXCEPT
SELECT "Id", "Created" FROM "Images" where "ImageOptimisationPolicy" <>  'use-original' AND "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False
    ORDER BY "Created";


-- retrieves all assets, while excluding the channel it should be on, this shows if there's anything unexpected

SELECT "Images"."Id" FROM "Images"
         JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
                     where "MediaType" LIKE 'image/%'
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels" where "Channel" = 'thumbs';

SELECT "Images"."Id" FROM "Images"
         JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
                     where "MediaType" LIKE 'image/%'
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels" where "Channel" = 'iiif-img';

-- shows if all assets have image delivery channels attached - check if not 0

SELECT count(*) FROM "Images" JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId" where IDC."Id" = null;
