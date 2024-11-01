using System;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Parsing;

namespace DLCS.Repository.NamedQueries;

/// <summary>
/// The type of projection target for named query
/// </summary>
public enum NamedQueryType
{
    /// <summary>
    /// NamedQuery will be projected to IIIF description resource 
    /// </summary>
    IIIF,
    
    /// <summary>
    /// NamedQuery will be projected to PDF object
    /// </summary>
    PDF,
    
    /// <summary>
    /// NamedQuery will be projected to ZIP archive containing images.
    /// </summary>
    Zip,
    
    /// <summary>
    /// NamedQuery will be projected to an array of matching assets
    /// </summary>
    Raw
}

public static class NamedQueryTypeDeriver
{
    /// <summary>
    /// Derive <see cref="NamedQueryType"/> from type of <see cref="ParsedNamedQuery"/>
    /// </summary>
    public static NamedQueryType GetNamedQueryParser<T>() where T : ParsedNamedQuery
    {
        if (typeof(T) == typeof(IIIFParsedNamedQuery)) return NamedQueryType.IIIF;
        if (typeof(T) == typeof(PdfParsedNamedQuery)) return NamedQueryType.PDF;
        if (typeof(T) == typeof(ZipParsedNamedQuery)) return NamedQueryType.Zip;
        if (typeof(T) == typeof(ParsedNamedQuery)) return NamedQueryType.Raw;

        throw new ArgumentOutOfRangeException(nameof(T), "Unable to determine NamedQueryType from result type");
    }
}

public delegate INamedQueryParser NamedQueryParserResolver(NamedQueryType projectionType);