using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Test.Helpers.Integration
{
    public static class DatabaseTestDataPopulation
    {
        public static ValueTask<EntityEntry<Asset>> AddTestAsset(this DbSet<Asset> assets,
            string id,
            AssetFamily family = AssetFamily.Image,
            int customer = 99,
            int space = 1,
            string origin = "",
            string roles = "",
            string mediaType = "image/jpeg",
            int maxUnauthorised = 0,
            int width = 8000,
            int height = 8000,
            string ref1 = "",
            string ref2 = "",
            string ref3 = "",
            int num1 = 0,
            int num2 = 0,
            int num3 = 0)
            => assets.AddAsync(new Asset
            {
                Created = DateTime.Now, Customer = customer, Space = space, Id = id, Origin = origin,
                Width = width, Height = height, Roles = roles, Family = family, MediaType = mediaType,
                ThumbnailPolicy = "default", MaxUnauthorised = maxUnauthorised, Reference1 = ref1,
                Reference2 = ref2, Reference3 = ref3, NumberReference1 = num1, NumberReference2 = num2,
                NumberReference3 = num3
            });

        public static ValueTask<EntityEntry<AuthToken>> AddTestToken(this DbSet<AuthToken> authTokens,
            int customer = 99, int ttl = 100, DateTime? expires = null, string? sessionUserId = null,
            DateTime? lastChecked = null)
            => authTokens.AddAsync(
                new AuthToken
                {
                    Id = Guid.NewGuid().ToString(),
                    Created = DateTime.Now,
                    LastChecked = lastChecked ?? DateTime.Now,
                    CookieId = Guid.NewGuid().ToString(),
                    SessionUserId = sessionUserId ?? Guid.NewGuid().ToString(),
                    BearerToken = Guid.NewGuid().ToString().Replace("-", string.Empty),
                    Customer = customer,
                    Expires = expires ?? DateTime.Now.AddSeconds(ttl),
                    Ttl = ttl
                });

        public static ValueTask<EntityEntry<SessionUser>> AddTestSession(this DbSet<SessionUser> sessionUsers,
            List<string> roles, int customer = 99)
            => sessionUsers.AddAsync(
                new SessionUser
                {
                    Id = Guid.NewGuid().ToString(),
                    Created = DateTime.Now,
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
    }
}