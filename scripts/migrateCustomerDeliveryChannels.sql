-- create custom thumbnail policies based on the thumbnail policies table

INSERT INTO "DeliveryChannelPolicies" ("Name", "DisplayName", "Customer", "Channel", "System", "Created", "Modified", "PolicyData")
SELECT  p."Id", p."Name", 1, 'thumbs', false, current_timestamp, current_timestamp, n.new_sizes
FROM (SELECT '{[' || string_agg('"!' || dimension || ',' || dimension, '",') || '"]}' new_sizes,
             "Id"
      FROM (SELECT unnest(string_to_array("Sizes", ',')) AS dimension, "Id" FROM "ThumbnailPolicies") AS x
      GROUP BY "Id") AS n
         INNER JOIN "ThumbnailPolicies" p ON n."Id" = p."Id"
ON CONFLICT ("Customer", "Name", "Channel") DO UPDATE SET "PolicyData" = excluded."PolicyData";

-- create policies for customers

-- this is basically setting values in the table, based on other values in the table for other customers
-- i.e.: if there's a default-audio policy (with System: false), it will create a default-audio policy
-- for all customers in the customer table (except customer 1)
INSERT INTO "DeliveryChannelPolicies" ("Name", "DisplayName", "Customer", "Channel", "System", "Created", "Modified", "PolicyData")
SELECT table_2."Name", table_2."DisplayName", customers."Id", table_2."Channel", table_2."System", table_2."Modified", table_2."Modified", table_2."PolicyData"
FROM "DeliveryChannelPolicies" as table_2, "Customers" AS customers
WHERE customers."Id" <> 1 AND table_2."System" = false;

-- create default channels

BEGIN TRANSACTION;

-- this is inserting default delivery channels into the table based on what customer 1 (the admin customer) has
-- table_1 and table_2 are the same table
INSERT INTO "DefaultDeliveryChannels" AS table_1 ("Id", "Customer", "Space", "MediaType", "DeliveryChannelPolicyId")
SELECT gen_random_uuid(),customers."Id", 0, table_2."MediaType", table_2."DeliveryChannelPolicyId"
FROM "DefaultDeliveryChannels" as table_2, "Customers" AS customers
WHERE table_2."Space" = 0 AND table_2."Customer" = 1 AND customers."Id" <> 1;

-- The next 3 queries are updating specific DDC to use the created System:false policies that were inserted
-- when creating policies as the insert query above this, creates DDC linking JUST to policies owned by customer 1

-- if you had customer 2, this would update the DDC entry for default-audio to use the DeliveryChannelPolicy created
-- above, instead of the policy used by customer 1
UPDATE "DefaultDeliveryChannels"
SET "DeliveryChannelPolicyId" = joined_table."Id"
FROM (SELECT "DeliveryChannelPolicies"."Id", DDC."Customer" FROM "DeliveryChannelPolicies"
    JOIN public."DefaultDeliveryChannels" DDC on "DeliveryChannelPolicies"."Id" = DDC."DeliveryChannelPolicyId"
         WHERE "Name" = 'default-audio' AND "MediaType" = 'audio/*') as joined_table
WHERE "DefaultDeliveryChannels"."Customer" = joined_table."Customer" AND "DefaultDeliveryChannels"."Customer" <> 1
  AND "MediaType" = 'audio/*';

UPDATE "DefaultDeliveryChannels"
SET "DeliveryChannelPolicyId" = joined_table."Id"
FROM (SELECT "DeliveryChannelPolicies"."Id", DDC."Customer" FROM "DeliveryChannelPolicies"
    JOIN public."DefaultDeliveryChannels" DDC on "DeliveryChannelPolicies"."Id" = DDC."DeliveryChannelPolicyId"
         WHERE "Name" = 'default-video' AND "MediaType" = 'video/*') as joined_table
WHERE "DefaultDeliveryChannels"."Customer" = joined_table."Customer" AND "DefaultDeliveryChannels"."Customer" <> 1
  AND "MediaType" = 'video/*';

UPDATE "DefaultDeliveryChannels"
SET "DeliveryChannelPolicyId" = joined_table."Id"
FROM (SELECT "DeliveryChannelPolicies"."Id", DDC."Customer" FROM "DeliveryChannelPolicies"
    JOIN public."DefaultDeliveryChannels" DDC on "DeliveryChannelPolicies"."Customer" = DDC."Customer"
         WHERE "Name" = 'default' AND "MediaType" = 'image/*' AND "Channel" = 'thumbs' AND "DeliveryChannelPolicyId" = 3) -- is this correct? - will always be 3 as it's set on a migration, but could be made more flexible with a SELECT
    as joined_table
WHERE "DefaultDeliveryChannels"."Customer" = joined_table."Customer" AND "DefaultDeliveryChannels"."Customer" <> 1
  AND "MediaType" = 'image/*' AND "DeliveryChannelPolicyId" = 3;

COMMIT;


SELECT * FROM "DeliveryChannelPolicies"
    JOIN public."DefaultDeliveryChannels" DDC on "DeliveryChannelPolicies"."Id" = DDC."DeliveryChannelPolicyId"
         WHERE "Name" = 'default' AND "MediaType" = 'image/*' AND "Channel" = 'thumbs' AND "DeliveryChannelPolicyId" = 3; -- is this correct? - will always be 3 based on a migration, but could be made more flexible with a SELECT

SELECT * FROM "DefaultDeliveryChannels"
    JOIN public."DeliveryChannelPolicies" DCP on DCP."Id" = "DefaultDeliveryChannels"."DeliveryChannelPolicyId" WHERE "Name" = 'default-audio';

SELECT * FROM "DefaultDeliveryChannels"
    JOIN public."DeliveryChannelPolicies" DCP on DCP."Id" = "DefaultDeliveryChannels"."DeliveryChannelPolicyId" WHERE "Name" = 'default';

SELECT * FROM "DefaultDeliveryChannels"
    JOIN public."DeliveryChannelPolicies" DCP on DCP."Id" = "DefaultDeliveryChannels"."DeliveryChannelPolicyId" WHERE "Channel" = 'file';

SELECT * FROM "DefaultDeliveryChannels"
    JOIN public."DeliveryChannelPolicies" DCP on DCP."Id" = "DefaultDeliveryChannels"."DeliveryChannelPolicyId" WHERE "Channel" = 'thumbs';