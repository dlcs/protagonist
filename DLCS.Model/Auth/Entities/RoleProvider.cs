#nullable disable

namespace DLCS.Model.Auth.Entities
{
    public partial class RoleProvider
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public string AuthService { get; set; }
        public string Configuration { get; set; }
        public string Credentials { get; set; }
    }
}
