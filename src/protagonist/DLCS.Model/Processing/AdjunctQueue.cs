namespace DLCS.Model.Processing;

public class AdjunctQueue
{
    public int Customer { get; set; }
    public int Size { get; set; }
    public long BatchesWaiting { get; set; }
    public long AdjunctsWaiting { get; set; }
}
