using System.Collections.Generic;

namespace DLCS.Core.Types;

public class ResourceType
{
    public static string Image { get; } = "Image";
    
    public static string Sound { get; } = "Sound";
    
    public static string Video { get; } = "Video";
    
    public static string Text { get; } = "Text";
    
    public static string Model { get; } = "model";
    
    public static string Dataset { get; } = "Dataset";
    
    public static List<string> All { get; } = [Image, Sound, Video, Text, Model, Dataset];
}
