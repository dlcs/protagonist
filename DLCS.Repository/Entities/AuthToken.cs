using System;

#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class AuthToken
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
        public DateTime? LastChecked { get; set; }
        public string CookieId { get; set; }
        public string SessionUserId { get; set; }
        public string BearerToken { get; set; }
        public int Ttl { get; set; }
    }
}
