using System;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Portal.TagHelpers;

[HtmlTargetElement("iiif-thumb", TagStructure = TagStructure.WithoutEndTag)]
public class DlcsIIIFThumbnailTagHelper : TagHelper
{
    public Image ApiImage { get; set; }
    public DlcsSettings Settings { get; set; }
    
    // This is the scaled down size for the img width and height attrs
    
    public int Small { get; set; }
    // This is the actual image request, which must be a supported thumb size
    public int Large { get; set; }
    public string CustomerUrlPart { get; set; }
    public string SpaceUrlPart { get; set; }
    
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var smallThumb = GetThumbnail(Small);
        var largeThumb = GetThumbnail(Large);
        output.TagName = "img";
        output.Attributes.SetAttribute("width", smallThumb.Width);
        output.Attributes.SetAttribute("height", smallThumb.Height);
        //output.Attributes.SetAttribute("data-src", smallThumb.Src);
        // TODO - add unveil functionality
        output.Attributes.SetAttribute("src", smallThumb.Src);
        output.Attributes.SetAttribute("data-placement", "auto");
        output.Attributes.SetAttribute("data-iiif", GetImageApi("iiif-img"));
        string title =
            $"<img src='{largeThumb.Src}' width={largeThumb.Width} height={largeThumb.Height} />";
        output.Attributes.SetAttribute("title", title);
    }
    
    private Thumbnail GetThumbnail(int boundingSize)
    {    
        if (!ApiImage.Width.HasValue || !ApiImage.Height.HasValue)
        {
            return new Thumbnail
            {
                Width = boundingSize,
                Height = boundingSize,
                Src = "/dash/img/placeholder.png"
            };
        }

        var src = $"{GetImageApi("thumbs")}/full/!{Large},{Large}/0/default.jpg";
        if (ApiImage.Width <= boundingSize && ApiImage.Height <= boundingSize)
        {
            return new Thumbnail
            {
                Width = boundingSize,
                Height = boundingSize,
                Src = src
            };
        }
        var scaleW = boundingSize / (double)ApiImage.Width;
        var scaleH = boundingSize / (double)ApiImage.Height;
        var scale = Math.Min(scaleW, scaleH);
        return new Thumbnail
        {
            Width = (int)Math.Round((ApiImage.Width.Value * scale)),
            Height = (int)Math.Round((ApiImage.Height.Value * scale)),
            Src = src
        };
    }

    private string GetImageApi(string type)
    {
        var id = ApiImage.GetLastPathElement();
        return $"{Settings.ResourceRoot}{type}/{CustomerUrlPart}/{SpaceUrlPart}/{id}";
    }
}


public class Thumbnail
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string Src { get; set; }
}