#nullable disable

using System;

namespace DLCS.Model.Customers;

public partial class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string[] Keys { get; set; }
    public bool Administrator { get; set; }
    public DateTime Created { get; set; }
    public bool AcceptedAgreement { get; set; }
}
