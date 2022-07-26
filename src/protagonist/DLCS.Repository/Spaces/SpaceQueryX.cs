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
    /// <param name="descending"></param>
    /// <returns></returns>
    public static IQueryable<Space> AsOrderedSpaceQuery(
        this IQueryable<Space> spaceQuery, 
        string orderBy,
        bool descending = false)
    {
        if (orderBy.HasText())
        {
            var field = orderBy.ToLowerInvariant();
            switch (field)
            {
                case "name":
                    return descending ? spaceQuery.OrderByDescending(s => s.Name) : spaceQuery.OrderBy(s => s.Name);
                case "created":
                    return descending ? spaceQuery.OrderByDescending(s => s.Created) : spaceQuery.OrderBy(s => s.Created);
            }
        }

        return spaceQuery;
    }
}