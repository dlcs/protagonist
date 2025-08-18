# Scripts

A collection of scripts for general querying/migrations/data manipulation etc.

## Delivery Channels

Scripts related to introduction of DeliveryChannels tables, see RFC [014-delivery-channels-database.md](../docs/rfcs/014-delivery-channels-database.md) for more information.

> [!WARNING]  
> The migration scripts need to be run in order.

* [0001-migrateCustomerDeliveryChannels.sql](DeliveryChannels/0001-migrateCustomerDeliveryChannels.sql) - Create required `DefaultDeliveryChannels` and `DeliveryChannelPolicies` for all customers from legacy `ThumbnailPolicy` and system `DeliveryChannelPolicies`
* [0002-migrateImageDeliveryChannels.sql](DeliveryChannels/0002-migrateImageDeliveryChannels.sql) - Create `ImageDeliveryChannels` records for all customers.
* [0003-deliveryChannelValidations.sql](DeliveryChannels/0003-deliveryChannelValidations.sql) - Various exploratory validation queries for delivery channels.
