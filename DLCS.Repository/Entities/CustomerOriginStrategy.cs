#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class CustomerOriginStrategy
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public string Regex { get; set; }
        public string Strategy { get; set; }
        public string Credentials { get; set; }
        public bool Optimised { get; set; }
    }
}
