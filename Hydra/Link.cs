namespace Hydra
{
    /// <summary>
    /// Coerced @id link
    /// </summary>
    public class Link : JSONLDBase
    {
        public override string Type
        {
            get { return "@id"; }
        }
    }
}