using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Ids = DLCS.Model.Policies.KnownDeliveryChannelPolicies;

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
                    { Ids.ImageDefault, "iiif-img", DateTime.UtcNow, 1, "A default image policy", DateTime.UtcNow, "default", null, true },
                    { Ids.ImageUseOriginal, "iiif-img", DateTime.UtcNow, 1, "Use original at Image Server", DateTime.UtcNow, "use-original", null, true },
                    { Ids.ThumbsDefault, "thumbs", DateTime.UtcNow, 1, "A default thumbs policy", DateTime.UtcNow, "default", "[\"!1024,1024\", \"!400,400\", \"!200,200\", \"!100,100\"]", false },
                    { Ids.FileNone, "file", DateTime.UtcNow, 1, "No transformations", DateTime.UtcNow, "none", null, true },
                    { Ids.AvDefaultAudio, "iiif-av", DateTime.UtcNow, 1, "A default audio policy", DateTime.UtcNow, "default-audio", "[\"audio-mp3-128\"]", false },
                    { Ids.AvDefaultVideo, "iiif-av", DateTime.UtcNow, 1, "A default video policy", DateTime.UtcNow, "default-video", "[\"video-mp4-720p\"]", false },
                    { Ids.None, "none", DateTime.UtcNow, 1, "Empty channel", DateTime.UtcNow, "none", null, true }
                });
          
            migrationBuilder.InsertData(
                table: "DefaultDeliveryChannels",
                columns: new[] { "Id", "Customer", "DeliveryChannelPolicyId", "MediaType", "Space" },
                values: new object[,]
                {
                    { Guid.NewGuid(), 1, Ids.FileNone, "application/*", 0 },
                    { Guid.NewGuid(), 1, Ids.ImageDefault, "image/*", 0 },
                    { Guid.NewGuid(), 1, Ids.AvDefaultVideo, "video/*", 0 },
                    { Guid.NewGuid(), 1, Ids.AvDefaultAudio, "audio/*", 0 },
                    { Guid.NewGuid(), 1, Ids.ThumbsDefault, "image/*", 0 }
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
