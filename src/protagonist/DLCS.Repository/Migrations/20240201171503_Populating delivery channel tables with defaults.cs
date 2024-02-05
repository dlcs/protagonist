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
                    { 1, "iiif-img", DateTime.UtcNow, 1, "A default image policy", DateTime.UtcNow, "default", null, true },
                    { 2, "iiif-img", DateTime.UtcNow, 1, "Use original at Image Server", DateTime.UtcNow, "use-original", null, true },
                    { 3, "thumbs", DateTime.UtcNow, 1, "A default thumbs policy", DateTime.UtcNow, "default", "[\"!1024,1024\", \"!400,400\", \"!200,200\", \"!100,100\"]", false },
                    { 4, "file", DateTime.UtcNow, 1, "No transformations", DateTime.UtcNow, "none", null, true },
                    { 5, "iiif-av", DateTime.UtcNow, 1, "A default audio policy", DateTime.UtcNow, "default-audio", "[\"audio-mp3-128\"]", false },
                    { 6, "iiif-av", DateTime.UtcNow, 1, "A default video policy", DateTime.UtcNow, "default-video", "[\"video-mp4-720p\"]", false },
                    { 7, "none", DateTime.UtcNow, 1, "Empty channel", DateTime.UtcNow, "none", null, true }
                });
          
            migrationBuilder.InsertData(
                table: "DefaultDeliveryChannels",
                columns: new[] { "Id", "Customer", "DeliveryChannelPolicyId", "MediaType", "Space" },
                values: new object[,]
                {
                    { Guid.NewGuid(), 1, 4, "application/*", 0 },
                    { Guid.NewGuid(), 1, 1, "image/*", 0 },
                    { Guid.NewGuid(), 1, 6, "video/*", 0 },
                    { Guid.NewGuid(), 1, 5, "audio/*", 0 },
                    { Guid.NewGuid(), 1, 3, "image/*", 0 }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
TRUNCATE ""ImageDeliveryChannels"", ""DefaultDeliveryChannels"", ""DeliveryChannelPolicies""  RESTART IDENTITY;
");
        }
    }
}
