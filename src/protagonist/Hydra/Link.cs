namespace Hydra;

/// <summary>
/// Coerced @id link
/// </summary>
public class Link : JsonLdBase
{
    public override string Type
    {
        get { return "@id"; }
    }
}