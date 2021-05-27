using System;

#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class Space
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Customer { get; set; }
        public DateTime Created { get; set; }
        public string ImageBucket { get; set; }
        public string Tags { get; set; }
        public string Roles { get; set; }
        public bool Keep { get; set; }
        public bool Transform { get; set; }
        public int MaxUnauthorised { get; set; }
    }
}
