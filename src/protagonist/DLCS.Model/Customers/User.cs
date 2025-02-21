#nullable disable

using System;

namespace DLCS.Model.Customers;

public partial class User
{
    public string Id { get; set; }
    public int Customer { get; set; }
    public string Email { get; set; }
    public string EncryptedPassword { get; set; }
    public DateTime Created { get; set; }
    public bool Enabled { get; set; }
    public string Roles { get; set; }
}
