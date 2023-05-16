using System.Text;
using DLCS.Core.Guard;

namespace DLCS.Core.Encryption;

/// <summary>
/// Implementation of <see cref="IEncryption"/> using SHA256 algorithm.
/// </summary>
public class SHA256 : IEncryption
{
    /// <summary>
    /// Encrypt specified string using SHA256 algorithm
    /// </summary>
    /// <returns>SHA256 encrypted string</returns>
    /// <remarks>This mimics deliverator DLCS implementation as we are sharing same datasource.</remarks>
    public string Encrypt(string source)
    {
        source.ThrowIfNullOrWhiteSpace(nameof(source));
        
        var hash = GenerateHash(source);
        StringBuilder s = new();
        foreach (byte b in hash)
        {
            s.Append(b.ToString("x2").ToLower());
        }
        return s.ToString();
    }

    private static byte[] GenerateHash(string source)
    {
        byte[] bs = Encoding.UTF8.GetBytes(source);
        var hash = System.Security.Cryptography.SHA256.HashData(bs);
        return hash;
    }
}