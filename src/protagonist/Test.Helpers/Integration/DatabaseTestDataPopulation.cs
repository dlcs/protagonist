using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.Assets.Metadata;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Customers;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Policies;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using DLCS.Repository.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Test.Helpers.Data;

namespace Test.Helpers.Integration;

public static class DatabaseTestDataPopulation
{
    public static ValueTask<EntityEntry<Asset>> AddTestAsset(this DbSet<Asset> assets,
        AssetId id,
        AssetFamily family = AssetFamily.Image,
        int customer = 99,
        int space = 1,
        string origin = "http://test",
        string roles = "",
        string mediaType = "image/jpeg",
        int maxUnauthorised = -1,
        int width = 8000,
        int height = 8000,
        string ref1 = "",
        string ref2 = "",
        string ref3 = "",
        int num1 = 0,
        int num2 = 0,
        int num3 = 0,
        bool notForDelivery = false,
        int batch = 0,
        long duration = 0,
        bool ingesting = false,
        string error = "",
        string imageOptimisationPolicy = "",
        DateTime? finished = null,
        List<ImageDeliveryChannel> imageDeliveryChannels = null)
    {
        return assets.AddAsync(new Asset
        {
            Created = DateTime.UtcNow, Customer = customer, Space = space, Id = id, Origin = origin,
            Width = width, Height = height, Roles = roles, Family = family, MediaType = mediaType,
            ThumbnailPolicy = "default", MaxUnauthorised = maxUnauthorised,
            Reference1 = ref1, Reference2 = ref2, Reference3 = ref3,
            NumberReference1 = num1, NumberReference2 = num2, NumberReference3 = num3,
            NotForDelivery = notForDelivery, Tags = "", PreservedUri = "", Error = error,
            ImageOptimisationPolicy = imageOptimisationPolicy, Batch = batch, Ingesting = ingesting,
            Duration = duration, Finished = finished,
            ImageDeliveryChannels = imageDeliveryChannels ?? new List<ImageDeliveryChannel>()
        });
    }

    public static ValueTask<EntityEntry<AuthToken>> AddTestToken(this DbSet<AuthToken> authTokens,
        int customer = 99, int ttl = 100, DateTime? expires = null, string? sessionUserId = null,
        DateTime? lastChecked = null)
        => authTokens.AddAsync(
            new AuthToken
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.UtcNow,
                LastChecked = lastChecked ?? DateTime.UtcNow,
                CookieId = Guid.NewGuid().ToString(),
                SessionUserId = sessionUserId ?? Guid.NewGuid().ToString(),
                BearerToken = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Customer = customer,
                Expires = expires ?? DateTime.UtcNow.AddSeconds(ttl),
                Ttl = ttl
            });

    public static ValueTask<EntityEntry<SessionUser>> AddTestSession(this DbSet<SessionUser> sessionUsers,
        List<string> roles, int customer = 99)
        => sessionUsers.AddAsync(
            new SessionUser
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.UtcNow,
                Roles = new Dictionary<int, List<string>>
                {
                    [customer] = roles
                }
            });

    public static ValueTask<EntityEntry<NamedQuery>> AddTestNamedQuery(this DbSet<NamedQuery> namedQueries,
        string name, int customer = 99, string template = "manifest=s3&canvas=n2&space=p1", bool global = true)
        => namedQueries.AddAsync(
            new NamedQuery
            {
                Id = Guid.NewGuid().ToString(),
                Customer = customer,
                Global = global,
                Name = name,
                Template = template
            });

    public static ValueTask<EntityEntry<CustomHeader>> AddTestCustomHeader(this DbSet<CustomHeader> customHeaders,
        string key, string value, int customer = 99, int space = 1)
        => customHeaders.AddAsync(
            new CustomHeader
            {
                Id = Guid.NewGuid().ToString(),
                Customer = customer,
                Space = space,
                Key = key,
                Value = value
            });

    public static ValueTask<EntityEntry<Space>> AddTestSpace(this DbSet<Space> spaces,
        int customer, int id, string name = null) =>
        spaces.AddAsync(new Space { Customer = customer, Id = id, Name = name ?? id.ToString() });

    public static ValueTask<EntityEntry<Customer>> AddTestCustomer(this DbSet<Customer> customers,
        int id, string name = null, string displayName = null) =>
        customers.AddAsync(new Customer
        {
            Id = id, Name = name ?? id.ToString(), Keys = Array.Empty<string>(),
            DisplayName = displayName ?? id.ToString(), Created = DateTime.UtcNow
        });

    public static Task AddTestDefaultDeliveryChannels(this DbSet<DefaultDeliveryChannel> defaultDeliveryChannels,
        int customerId) =>
        defaultDeliveryChannels.AddRangeAsync(defaultDeliveryChannels.Where(d => d.Customer == 1 && d.Space == 0)
            .Select(x => new DefaultDeliveryChannel()
            {
                Customer = customerId,
                Space = x.Space,
                MediaType = x.MediaType,
                DeliveryChannelPolicyId = x.DeliveryChannelPolicyId
            }));

    public static ValueTask<EntityEntry<User>> AddTestUser(this DbSet<User> users,
        int customer, string email, string password = "password123") => users.AddAsync(new User
    {
        Id = Guid.NewGuid().ToString(),
        Customer = customer,
        Email = email,
        EncryptedPassword = "ENCRYPTED " + password,
        Enabled = true,
        Created = DateTime.UtcNow,
        Roles = string.Empty
    });

    public static ValueTask<EntityEntry<ImageLocation>> AddTestImageLocation(this DbSet<ImageLocation> locations,
        AssetId id, string s3 = "s3://wherever", string nas = "")
        => locations.AddAsync(new ImageLocation { Id = id, S3 = s3, Nas = nas });

    public static ValueTask<EntityEntry<ImageStorage>> AddTestImageStorage(this DbSet<ImageStorage> storage,
        AssetId id, int space = 1, int customer = 99, long size = 123, long thumbSize = 10)
        => storage.AddAsync(new ImageStorage
        {
            Id = id,
            Customer = customer,
            Space = space,
            Size = size,
            LastChecked = DateTime.UtcNow.AddDays(-7),
            ThumbnailSize = thumbSize
        });

    public static ValueTask<EntityEntry<Batch>> AddTestBatch(this DbSet<Batch> batch, int id, int customer = 99,
        int count = 1, int completed = 0, int errors = 0, DateTime? submitted = null, bool superseded = false,
        DateTime? finished = null)
        => batch.AddAsync(new Batch
        {
            Id = id, Customer = customer, Submitted = submitted ?? DateTime.UtcNow, Completed = completed,
            Count = count, Errors = errors, Superseded = superseded, Finished = finished
        });

    public static ValueTask<EntityEntry<CustomerStorage>> AddTestCustomerStorage(
        this DbSet<CustomerStorage> customerStorages, int customer = 99, int space = 0, int numberOfImages = 0,
        long sizeOfStored = 0, long sizeOfThumbs = 0, string storagePolicy = "default")
        => customerStorages.AddAsync(new CustomerStorage
        {
            Customer = customer,
            Space = space,
            LastCalculated = DateTime.UtcNow,
            StoragePolicy = storagePolicy,
            NumberOfStoredImages = numberOfImages,
            TotalSizeOfStoredImages = sizeOfStored,
            TotalSizeOfThumbnails = sizeOfThumbs
        });

    public static ValueTask<EntityEntry<AssetApplicationMetadata>> AddTestAssetApplicationMetadata(
        this DbSet<AssetApplicationMetadata> assetApplicationMetadata, AssetId assetId,
        string metadataType, string metadataValue)
        => assetApplicationMetadata.AddAsync(new AssetApplicationMetadata()
        {
            AssetId = assetId,
            MetadataType = metadataType,
            MetadataValue = metadataValue,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        });

    public static ValueTask<EntityEntry<Asset>> WithTestThumbnailMetadata(
        this ValueTask<EntityEntry<Asset>> asset,
        string metadataValue = "{\"a\": [], \"o\": [[769,1024],[300,400],[150,200],[75,100]]}")
    {
        asset.Result.Entity.WithTestThumbnailMetadata(metadataValue);
        return asset;
    }

    public static ValueTask<EntityEntry<Asset>> WithTestDeliveryChannel(
        this ValueTask<EntityEntry<Asset>> asset,
        string deliveryChannel,
        int? policyId = null)
    {
        asset.Result.Entity.ImageDeliveryChannels.Add(new ImageDeliveryChannel()
        {
            Channel = deliveryChannel,
            DeliveryChannelPolicyId = policyId ?? deliveryChannel switch
            {
                AssetDeliveryChannels.Image => KnownDeliveryChannelPolicies.ImageDefault,
                AssetDeliveryChannels.Thumbnails => KnownDeliveryChannelPolicies.ThumbsDefault,
                AssetDeliveryChannels.Timebased => MIMEHelper.IsVideo(asset.Result.Entity.Origin)
                    ? KnownDeliveryChannelPolicies.AvDefaultVideo
                    : KnownDeliveryChannelPolicies.AvDefaultAudio,
                AssetDeliveryChannels.File => KnownDeliveryChannelPolicies.FileNone,
                _ => throw new ArgumentOutOfRangeException(nameof(deliveryChannel), deliveryChannel,
                    $"Unable to assign default delivery channel policy for channel {deliveryChannel} for asset")
            }
        });
        
        return asset;
    }
}