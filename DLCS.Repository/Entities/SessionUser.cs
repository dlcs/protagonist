using System;

#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class SessionUser
    {
        public string Id { get; set; }
        public DateTime Created { get; set; }
        public string Roles { get; set; }
    }
}
