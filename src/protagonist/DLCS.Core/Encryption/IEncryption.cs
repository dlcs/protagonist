namespace DLCS.Core.Encryption
{
    /// <summary>
    /// Interface for operations related to encryption
    /// </summary>
    public interface IEncryption
    {
        /// <summary>
        /// Encrypt source string and return encrypted string
        /// </summary>
        /// <returns>Encrypted string</returns>
        string Encrypt(string source);
    }
}