using System;

namespace Portal.Features.Account.Models;

public class SignupModel
{
    public string? Id { get; set; }
    public DateTime Created { get; set; }
    public DateTime Expires { get; set; }
    public string? Note { get; set; }
    public string? CustomerName { get; set; }
    public int? CustomerId { get; set; }
    public string? CssClass { get; set; }
}