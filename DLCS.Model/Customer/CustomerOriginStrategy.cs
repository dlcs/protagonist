#nullable disable

namespace DLCS.Model.Customer
{
    public partial class CustomerOriginStrategy
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public string Regex { get; set; }
        public OriginStrategyType Strategy { get; set; }
        public string Credentials { get; set; }
        public bool Optimised { get; set; }
    }
}
