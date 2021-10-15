using System;

namespace API.Client.OldJsonLd
{
    /// <summary>
    /// Hydra entity representing User of Portal UI.
    /// </summary>
    public class PortalUser : OldJsonLdBase
    {
        public string Email { get; set; }
        public DateTime? Created { get; set; }
        public bool Enabled { get; set; }
        public string? Password { get; set; }
    }
}