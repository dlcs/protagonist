namespace DLCS.Model.Assets
{
    /// <summary>
    /// Represents the family of an asset in DLCS.
    /// </summary>
    public enum AssetFamily
    {
        /// <summary>
        /// Represents an image asset.
        /// </summary>
        Image = 'I',
        
        /// <summary>
        /// Represents a time based asset (audio or video).
        /// </summary>
        Timebased = 'T',
        
        /// <summary>
        /// Represents a file asset (pdf, docx etc).
        /// </summary>
        File = 'F'
    }
}