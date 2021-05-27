using System;

#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class Batch
    {
        public int Id { get; set; }
        public int Customer { get; set; }
        public DateTime Submitted { get; set; }
        public int Count { get; set; }
        public int Completed { get; set; }
        public int Errors { get; set; }
        public DateTime? Finished { get; set; }
        public bool Superseded { get; set; }
    }
}
