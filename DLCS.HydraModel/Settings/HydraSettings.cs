namespace DLCS.HydraModel.Settings
{
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