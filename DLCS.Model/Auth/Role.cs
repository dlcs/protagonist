#nullable disable

namespace DLCS.Model.Auth
{
    public partial class Role
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public string AuthService { get; set; }
        public string Name { get; set; }
        public string Aliases { get; set; }
    }
}
