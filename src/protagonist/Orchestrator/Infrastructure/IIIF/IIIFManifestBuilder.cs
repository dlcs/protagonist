using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V2.Strings;
using IIIF.Presentation.V3.Strings;
using IIIF2 = IIIF.Presentation.V2;
using IIIF3 = IIIF.Presentation.V3;

namespace Orchestrator.Infrastructure.IIIF;

/// <summary>
/// Class for creating IIIF Manifests from provided assets 
/// </summary>
public class IIIFManifestBuilder
{
    private readonly IIIFCanvasFactory canvasFactory;

    public IIIFManifestBuilder(IIIFCanvasFactory canvasFactory) 
    {
        this.canvasFactory = canvasFactory;
    }

    public async Task<IIIF3.Manifest> GenerateV3Manifest(List<Asset> assets, CustomerPathElement customerPathElement,
        string manifestId, string label)
    {
        const string language = "en";
        var manifest = new IIIF3.Manifest
        {
            Id = manifestId,
            Label = new LanguageMap(language, label),
            Metadata = new List<LabelValuePair>
            {
                new(language, "Title", "Created by DLCS"),
                new(language, "Generated On", DateTime.UtcNow.ToString("u"))
            }
        };
        
        var canvases = await canvasFactory.CreateV3Canvases(assets, customerPathElement);
        manifest.Items = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
        
        manifest.EnsurePresentation3Context();
        return manifest;
    }

    public async Task<IIIF2.Manifest> GenerateV2Manifest(List<Asset> assets, CustomerPathElement customerPathElement,
        string manifestId, string label, string sequenceRoot)
    {
        var manifest = new IIIF2.Manifest
        {
            Id = manifestId,
            Label = new MetaDataValue(label),
            Metadata = new List<IIIF2.Metadata>
            {
                new()
                {
                    Label = new MetaDataValue("Title"),
                    Value = new MetaDataValue("Created by DLCS")
                } ,
                new()
                {
                    Label = new MetaDataValue("Generated On"),
                    Value = new MetaDataValue(DateTime.UtcNow.ToString("u"))
                }   
            }
        };
        
        var canvases = await canvasFactory.CreateV2Canvases(assets, customerPathElement);
        var sequence = new IIIF2.Sequence
        {
            Id = string.Concat(sequenceRoot, "/sequence/0"),
            Label = new MetaDataValue("Sequence 0"),
        };
        sequence.Canvases = canvases;
        manifest.Thumbnail = canvases.FirstOrDefault(c => !c.Thumbnail.IsNullOrEmpty())?.Thumbnail;
        manifest.Sequences = sequence.AsList();

        manifest.EnsurePresentation2Context();
        return manifest;
    }
}