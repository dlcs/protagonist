using System;
using System.Linq;
using DLCS.Core.Collections;

#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class Space
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Customer { get; set; }
        public DateTime Created { get; set; }
        public string ImageBucket { get; set; } = string.Empty;
        public string[] Tags { get; set; }
        public string[] Roles { get; set; }
        
        public bool Keep { get; set; }
        public bool Transform { get; set; }
        public int MaxUnauthorised { get; set; }
        
        public long ApproximateNumberOfImages { get; set; }
    }

    public static class SpaceX
    {
        /// <summary>
        /// Add specified tag to spaces tag list
        /// </summary>
        public static void AddTag(this Space space, string tag)
        {
            space.Tags = StringArrays.EnsureString(space.Tags, tag);
        }

        /// <summary>
        /// Remove specified tag from spaces tag list
        /// </summary>
        public static void RemoveTag(this Space space, string tag)
        {
            space.Tags = StringArrays.RemoveString(space.Tags, tag);
        }
    }
}
