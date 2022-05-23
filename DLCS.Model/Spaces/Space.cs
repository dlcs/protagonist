#nullable disable

using System;
using System.Linq;

namespace DLCS.Model.Spaces
{
    public partial class Space
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Customer { get; set; }
        public DateTime Created { get; set; }
        public string ImageBucket { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool Keep { get; set; }
        public bool Transform { get; set; }
        public int MaxUnauthorised { get; set; }
    }

    public static class SpaceX
    {
        /// <summary>
        /// Add specified tag to spaces tag list
        /// </summary>
        public static void AddTag(this Space space, string tag)
        {
            if (string.IsNullOrEmpty(space.Tags))
            {
                space.Tags = tag;
                return;
            }

            var tags = space.Tags.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList();
            if (tags.Contains(tag)) return;
            
            tags.Add(tag);
            space.Tags = string.Join(",", tags);
        }

        /// <summary>
        /// Remove specified tag from spaces tag list
        /// </summary>
        public static void RemoveTag(this Space space, string tag)
        {
            if (string.IsNullOrEmpty(space.Tags)) return;
            
            var tags = space.Tags.Split(",", StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!tags.Contains(tag)) return;

            space.Tags = string.Join(",", tags.Where(t => t != tag));
        }
    }
}
