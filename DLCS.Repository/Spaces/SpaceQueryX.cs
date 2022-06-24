using System.Linq;
using DLCS.Core.Strings;
using DLCS.Model.Spaces;

namespace DLCS.Repository.Spaces;

public static class SpaceQueryX
{
    /// <summary>
    /// Optionally adds ordering statements to the IQueryable
    /// </summary>
    /// <param name="spaceQuery"></param>
    /// <param name="orderBy"></param>
    /// <param name="ascending"></param>
    /// <returns></returns>
    public static IQueryable<Space> AsOrderedSpaceQuery(this IQueryable<Space> spaceQuery, string orderBy,
        bool ascending = true)
    {
        if (orderBy.HasText())
        {
            var field = orderBy.ToLowerInvariant();
            switch (field)
            {
                case "name":
                    return ascending ? spaceQuery.OrderBy(s => s.Name) : spaceQuery.OrderByDescending(s => s.Name);
                case "created":
                    return ascending ? spaceQuery.OrderBy(s => s.Created) : spaceQuery.OrderByDescending(s => s.Created);
            }
        }

        return spaceQuery;
    }
}