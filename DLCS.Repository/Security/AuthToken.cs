using System;

#nullable disable

namespace DLCS.Repository.Security
{
    /// <summary>
    /// Represents Cookie/BearerToken that grants the holder access to resources as specified in the related
    /// SessionUser (from SessionUserId).
    /// </summary>
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
