namespace DLCS.HydraModel.Settings
{
    // This class needs to go and Hydra objects get their base URL and Vocab by other means.
    public class HydraSettings
    {
        public string BaseUrl { get; set; }
        public string Vocab { get; set; }
        
        public string GetCustomerId(int internalId)
        {
            return $"{BaseUrl}/customer/{internalId}";
        }
    }
}