namespace DLCS.Model.Processing;

public class CustomerQueue
{
    public int Customer { get; set; }
    public int Size { get; set; }
    public string Name { get; set; } = "default";
    public long BatchesWaiting { get; set; } 
    public long ImagesWaiting { get; set; }
}