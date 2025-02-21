namespace DLCS.Model.Auth;

/// <summary>
/// Represents basic username and password credentials. 
/// </summary>
public class BasicCredentials
{
    public string User { get; set; }
    
    public string Password { get; set; }
}