-- queries that can be used to help verify changes based on the previous 2 scripts run
-- NOTE: some of these queries will stop working correctly when new data is added to the DB
-- this is due to certain fields like old deliveryChannels no longer being updated

-- general selects

SELECT count(*) FROM "Images";

SELECT count(*) FROM "ImageDeliveryChannels";


-- iif-img/thumbs selects - should provide numbers close to each other, but might not be the exact same

SELECT count(*) FROM "Images" where "MediaType" LIKE 'image/%';

SELECT count(*) FROM "ImageDeliveryChannels" WHERE "Channel" = 'iiif-img';

SELECT count(*) FROM "ImageDeliveryChannels" WHERE "Channel" = 'thumbs';

-- checking counts for any image that aren't use original

SELECT count(distinct("Id")) FROM "Images" WHERE "ImageOptimisationPolicy" <>  'use-original' AND "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False;
SELECT count(distinct("ImageId")) FROM "ImageDeliveryChannels"
    JOIN "DeliveryChannelPolicies" DCP ON DCP."Id" = "ImageDeliveryChannels"."DeliveryChannelPolicyId"
                WHERE "ImageDeliveryChannels"."Channel" = 'iiif-img'
                AND DCP."Name" <> 'use-original';

-- retrieves id of any image that doesn't have a related image delivery channel

SELECT "Id" FROM "Images" WHERE "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels"
    JOIN "DeliveryChannelPolicies" DCP ON DCP."Id" = "ImageDeliveryChannels"."DeliveryChannelPolicyId"
                WHERE "ImageDeliveryChannels"."Channel" = 'iiif-img';

-- retrieves images that should have a default policy

SELECT * FROM "Images"
            JOIN "ImageDeliveryChannels" IDC ON "Images"."Id" = IDC."ImageId"
            JOIN "DeliveryChannelPolicies" DCP ON DCP."Id" = IDC."DeliveryChannelPolicyId"
            WHERE "ImageOptimisationPolicy" <>  'use-original' AND "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False AND
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
SELECT "Id", "Created" FROM "Images" WHERE "ImageOptimisationPolicy" <>  'use-original'
                                       AND "DeliveryChannels" LIKE '%iiif-img%' AND "NotForDelivery" = False
    ORDER BY "Created";


-- retrieves the Id of assets that would be put into a channel, but aren't as they don't have an old delivery channel

SELECT "Id" FROM "Images" WHERE "MediaType" LIKE 'image/%' AND "NotForDelivery" = False
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels" WHERE "Channel" IN ('iiif-img', 'thumbs');

SELECT "Id" FROM "Images" WHERE "MediaType" LIKE ANY (array['audio/%', 'video%'])
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels" WHERE "Channel" = 'iiif-av';

-- retrieves the Id of assets that should have a delivery channel, but don't (requires investigation if any assets retrieved)

SELECT "Id" FROM "Images" WHERE "MediaType" LIKE 'image/%' AND "NotForDelivery" = False AND "DeliveryChannels" LIKE '%iiif-img%'
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels" WHERE "Channel" IN ('iiif-img', 'thumbs');

SELECT "Id" FROM "Images" WHERE "MediaType" LIKE ANY (array['audio/%', 'video%']) AND "DeliveryChannels" LIKE '%iiif-av%'
EXCEPT
SELECT "ImageId" FROM "ImageDeliveryChannels" WHERE "Channel" = 'iiif-av';

--- retrieves specific image and any IDC attached

SELECT * FROM "Images"
         LEFT JOIN "ImageDeliveryChannels" IDC ON "Images"."Id" = IDC."ImageId"
         WHERE "Images"."Id" = 'CHANGE ME';


-- shows the delta between assets that have image delivery channels attached and not

SELECT count(*) AS "No delivery channels" FROM "Images"
    LEFT JOIN "ImageDeliveryChannels" IDC ON "Images"."Id" = IDC."ImageId"
                WHERE IDC."Id" IS NULL;

-- delta as a percentage of all images

SELECT to_char(a.c::DECIMAL / b.c * 100, 'FM999999999.00') AS "delta percentage"
FROM (SELECT count(*) AS c FROM "Images"
    LEFT JOIN "ImageDeliveryChannels" IDC ON "Images"."Id" = IDC."ImageId"
                WHERE IDC."Id" IS NULL) AS a,
(SELECT count(*) AS c FROM "Images") AS b;

-- checking for delta on expected new + old on updated DDC policies - check if not 0

SELECT old.c - new.c as delta
FROM
(SELECT count(*) as c FROM "Images"
                JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
WHERE "Customer" <> 1 AND IDC."Channel" = 'iiif-av' AND IDC."DeliveryChannelPolicyId" <> 5) as new,
(SELECT count(*) as c FROM "Images"
WHERE "Customer" <> 1 AND "DeliveryChannels" LIKE '%iiif-av%' AND "MediaType" LIKE 'audio/%') as old;

SELECT old.c - new.c as delta
FROM
(SELECT count(*) as c FROM "Images"
                JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
WHERE "Customer" <> 1 AND IDC."Channel" = 'iiif-av' AND IDC."DeliveryChannelPolicyId" <> 6) as new,
(SELECT count(*) as c FROM "Images"
WHERE "Customer" <> 1 AND "DeliveryChannels" LIKE '%iiif-av%' AND "MediaType" LIKE 'video/%') as old;

SELECT old.c - new.c as delta
FROM
(SELECT count(*) as c FROM "Images"
                JOIN "ImageDeliveryChannels" IDC on "Images"."Id" = IDC."ImageId"
WHERE "Customer" <> 1 AND IDC."Channel" = 'thumbs' AND IDC."DeliveryChannelPolicyId" <> 3) as new,
(SELECT count(*) as c FROM "Images"
WHERE "Customer" <> 1 AND "DeliveryChannels" LIKE '%iiif-img%' AND "Family" = 'I'
  AND "NotForDelivery" = False) as old;
 