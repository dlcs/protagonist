#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class AuthService
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public string Name { get; set; }
        public string Profile { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string PageLabel { get; set; }
        public string PageDescription { get; set; }
        public string CallToAction { get; set; }
        public int Ttl { get; set; }
        public string RoleProvider { get; set; }
        public string ChildAuthService { get; set; }
    }
}
