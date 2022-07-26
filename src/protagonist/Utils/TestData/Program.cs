using System;
using DLCS.Repository;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

// TODO - create portal user and ingest image when API + Engine are appropriately developed
// TODO - customer / space creation should be replaced by API call when ready

Console.WriteLine("Creating test data...");
await using var context = DlcsContextConfiguration.GetNewDbContext(configuration);

var key = Guid.NewGuid().ToString();
Console.WriteLine($"Creating customer - (2 / \"Test\") with key {key}");
await context.Customers.AddAsync(new()
{
    Id = 2, AcceptedAgreement = true, Administrator = false, Created = DateTime.UtcNow, DisplayName = "test",
    Name = "test", Keys = new[] { key }
});

Console.WriteLine("Adding space for customer 2");
await context.Spaces.AddAsync(new() { Id = 1, Created = DateTime.UtcNow, Customer = 2, Name = "test" });

Console.WriteLine("Creating 'default' thumbnailPolicy");
await context.ThumbnailPolicies.AddAsync(new() { Id = "default", Name = "Test Policy", Sizes = "1000,400,200,100" });

Console.WriteLine("Creating image optimisation policies");
await context.ImageOptimisationPolicies.AddRangeAsync(
    new()
    {
        Id = "audio-max", Name = "Audio - mp3", TechnicalDetails = new[] { "System preset: Audio MP3 - 192k(mp3)" }
    },
    new() { Id = "fast-low", Name = "Fast low quality", TechnicalDetails = new[] { "kdu_low" } },
    new() { Id = "video-max", Name = "Video - mp4", TechnicalDetails = new[] { "System preset: Generic 1080p(mp4)" } });
    
Console.WriteLine("Creating origin strategies");
await context.OriginStrategies.AddRangeAsync(
    new() { Id = "s3-ambient", RequiresCredentials = false }, 
    new() { Id = "sftp", RequiresCredentials = true },
    new() { Id = "basic-http-authentication", RequiresCredentials = true });
    
Console.WriteLine("Saving changes..");
await context.SaveChangesAsync();