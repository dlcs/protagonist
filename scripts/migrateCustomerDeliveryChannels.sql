BEGIN TRANSACTION;

ALTER SEQUENCE "DeliveryChannelPolicies_Id_seq" RESTART WITH 8;

INSERT INTO "DeliveryChannelPolicies" ("Name", "DisplayName", "Customer", "Channel", "System", "Created", "Modified",
                                       "PolicyData")
SELECT TP."Id",
       TP."Name",
       1,
       'thumbs',
       false,
       current_timestamp,
       current_timestamp,
       n.new_sizes
FROM (SELECT '{[' || string_agg('"!' || dimension || ',' || dimension, '",') || '"]}' new_sizes,
             "Id"
      FROM (SELECT unnest(string_to_array("Sizes", ',')) AS dimension, "Id" FROM "ThumbnailPolicies") AS x
      GROUP BY "Id") AS n
         INNER JOIN "ThumbnailPolicies" TP ON n."Id" = TP."Id"
ON CONFLICT ("Customer", "Name", "Channel") DO UPDATE SET "PolicyData" = excluded."PolicyData",
                                                          "Modified"   = current_timestamp;

-- create policies for customers

-- this is basically setting values in the table, based on other values in the table for other customers
-- i.e.: if there's a default-audio policy (with System: false), it will create a default-audio policy
-- for all customers in the customer table (except customer 1)
INSERT INTO "DeliveryChannelPolicies" ("Name", "DisplayName", "Customer", "Channel", "System", "Created", "Modified",
                                       "PolicyData")
SELECT DDC."Name",
       DDC."DisplayName",
       C."Id",
       DDC."Channel",
       DDC."System",
       DDC."Modified",
       DDC."Modified",
       DDC."PolicyData"
FROM "DeliveryChannelPolicies" as DDC,
     "Customers" AS C
WHERE C."Id" <> 1
  AND DDC."System" = false;

-- create default channels

-- this is inserting default delivery channels into the table based on what customer 1 (the admin customer) has
INSERT INTO "DefaultDeliveryChannels" ("Id", "Customer", "Space", "MediaType", "DeliveryChannelPolicyId")
SELECT gen_random_uuid(), C."Id", 0, DDC."MediaType", DDC."DeliveryChannelPolicyId"
FROM "DefaultDeliveryChannels" as DDC,
     "Customers" AS C
WHERE DDC."Space" = 0
  AND DDC."Customer" = 1
  AND C."Id" <> 1;

-- The next 3 queries are updating specific DDC to use the created System:false policies that were inserted
-- when creating policies as the insert query above this, creates DDC linking JUST to policies owned by customer 1

-- if you had customer 2, this would update the DDC entry for default-audio to use the DeliveryChannelPolicy created
-- above, instead of the policy used by customer 1
UPDATE "DefaultDeliveryChannels" as DDC
SET "DeliveryChannelPolicyId" = DCP."Id"
FROM (SELECT "DeliveryChannelPolicies"."Id", DDC2."Customer"
      FROM "DeliveryChannelPolicies"
               JOIN public."DefaultDeliveryChannels" DDC2
                    on "DeliveryChannelPolicies"."Id" = DDC2."DeliveryChannelPolicyId"
      WHERE "Name" = 'default-audio'
        AND "MediaType" = 'audio/*') as DCP
WHERE DDC."Customer" = DCP."Customer"
  AND DDC."Customer" <> 1
  AND "MediaType" = 'audio/*';

UPDATE "DefaultDeliveryChannels" as DDC
SET "DeliveryChannelPolicyId" = DCP."Id"
FROM (SELECT "DeliveryChannelPolicies"."Id", DDC2."Customer"
      FROM "DeliveryChannelPolicies"
               JOIN public."DefaultDeliveryChannels" DDC2
                    on "DeliveryChannelPolicies"."Id" = DDC2."DeliveryChannelPolicyId"
      WHERE "Name" = 'default-video'
        AND "MediaType" = 'video/*') as DCP
WHERE DDC."Customer" = DCP."Customer"
  AND DDC."Customer" <> 1
  AND "MediaType" = 'video/*';

UPDATE "DefaultDeliveryChannels" AS DDC
SET "DeliveryChannelPolicyId" = DCP."Id"
FROM (SELECT "DeliveryChannelPolicies"."Id", DDC2."Customer"
      FROM "DeliveryChannelPolicies"
               JOIN public."DefaultDeliveryChannels" DDC2 on "DeliveryChannelPolicies"."Customer" = DDC2."Customer"
      WHERE "Name" = 'default'
        AND "MediaType" = 'image/*'
        AND "Channel" = 'thumbs'
        AND "DeliveryChannelPolicyId" = 3)
         as DCP
WHERE DDC."Customer" = DCP."Customer"
  AND DDC."Customer" <> 1
  AND "MediaType" = 'image/*'
  AND "DeliveryChannelPolicyId" = 3;

COMMIT;