using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DLCS.Repository.Migrations
{
    public partial class Populatingdeliverychanneltableswithdefaults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "DeliveryChannelPolicies",
                columns: new[] { "Id", "Channel", "Created", "Customer", "DisplayName", "Modified", "Name", "PolicyData", "System" },
                values: new object[,]
                {
                    { 1, "iiif-img", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4365), 1, "A default image policy", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4367), "default", null, true },
                    { 2, "iiif-img", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4369), 1, "Use original at Image Server", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4369), "use-original", null, true },
                    { 3, "thumbs", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4370), 1, "A default thumbs policy", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4370), "default", "[\"!1024,1024\", \"!400,400\", \"!200,200\", \"!100,100\"]", false },
                    { 4, "file", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4372), 1, "No transformations", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4372), "none", null, true },
                    { 5, "iiif-av", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4373), 1, "A default audio policy", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4374), "default-audio", "[\"audio-aac-192\"]", false },
                    { 6, "iiif-av", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4375), 1, "A default video policy", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4375), "default-video", "[\"video-mp4-720p\"]", false },
                    { 7, "none", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4376), 1, "Empty channel", new DateTime(2024, 1, 30, 15, 26, 50, 723, DateTimeKind.Utc).AddTicks(4376), "none", null, true }
                });

            migrationBuilder.InsertData(
                table: "DefaultDeliveryChannels",
                columns: new[] { "Id", "Customer", "DeliveryChannelPolicyId", "MediaType", "Space" },
                values: new object[,]
                {
                    { new Guid("0373c1e9-5e62-4c05-8295-23de029e0cd6"), 1, 5, "audio/*", 0 },
                    { new Guid("12534ee0-ba9f-4e4a-9bc1-d0a7123e7359"), 1, 1, "image/*", 0 },
                    { new Guid("563cb716-495c-49b1-a4a8-8351c78ec6e9"), 1, 3, "image/*", 0 },
                    { new Guid("96e9353e-1b16-4028-b986-0a47c1a6ea77"), 1, 4, "application/*", 0 },
                    { new Guid("9cf9cb13-3c3c-4411-8196-a51b40718296"), 1, 6, "video/*", 0 }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DefaultDeliveryChannels",
                keyColumn: "Id",
                keyValue: new Guid("0373c1e9-5e62-4c05-8295-23de029e0cd6"));

            migrationBuilder.DeleteData(
                table: "DefaultDeliveryChannels",
                keyColumn: "Id",
                keyValue: new Guid("12534ee0-ba9f-4e4a-9bc1-d0a7123e7359"));

            migrationBuilder.DeleteData(
                table: "DefaultDeliveryChannels",
                keyColumn: "Id",
                keyValue: new Guid("563cb716-495c-49b1-a4a8-8351c78ec6e9"));

            migrationBuilder.DeleteData(
                table: "DefaultDeliveryChannels",
                keyColumn: "Id",
                keyValue: new Guid("96e9353e-1b16-4028-b986-0a47c1a6ea77"));

            migrationBuilder.DeleteData(
                table: "DefaultDeliveryChannels",
                keyColumn: "Id",
                keyValue: new Guid("9cf9cb13-3c3c-4411-8196-a51b40718296"));

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "DeliveryChannelPolicies",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
