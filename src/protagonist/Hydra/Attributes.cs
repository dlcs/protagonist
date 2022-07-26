using System;

namespace Hydra;

/// <summary>
/// Provide a means for an attribute to point at another type
/// </summary>
public class TypeReferencingAttribute : Attribute
{
    public Type ReferencedType;

    public TypeReferencingAttribute(Type t)
    {
        ReferencedType = t;
    }
}

public class UnstableAttribute : Attribute
{
    public string Note { get; set; }
}

/// <summary>
/// Indicates that the class is fully described as a Hydra resource by the referenced Class
/// </summary>
public class HydraClassAttribute : TypeReferencingAttribute
{
    public HydraClassAttribute(Type t) : base(t) {}
    public string Description { get; set; }
    public string UriTemplate { get; set; }
}

/// <summary>
/// Base class for Hydra property attributes
/// </summary>
public class SupportedPropertyAttribute : Attribute
{
    public string Description { get; set; }
    public bool ReadOnly { get; set; }
    public bool WriteOnly { get; set; }
    public string Range { get; set; }
    public bool SetManually { get; set; }
}

/// <summary>
/// Indicates that the property is a field of the current resource - returned as value, rather than a hyperlink to another resource.
/// </summary>
public class RdfPropertyAttribute : SupportedPropertyAttribute { }


/// <summary>
/// Indicates that the property is a link to another resource, rather than a field of the current resource.
/// </summary>
public class HydraLinkAttribute : SupportedPropertyAttribute { }