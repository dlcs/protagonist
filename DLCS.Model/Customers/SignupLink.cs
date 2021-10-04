using System;

namespace DLCS.Model.Customers
{
    public partial class SignupLink
    {
        public string Id { get; set; }
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
        public string? Note { get; set; }
        public int? CustomerId { get; set; }
    }
}