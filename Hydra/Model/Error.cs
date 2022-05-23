namespace Hydra.Model
{
    /// <summary>
    /// This needs bringing up to date with the Hydra spec.
    /// For now this is mainly here to give us a JSON error message.
    /// </summary>
    public class Error : Status
    {
        public override string Type => "Error";
    }
}