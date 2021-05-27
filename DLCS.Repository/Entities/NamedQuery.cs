#nullable disable

namespace DLCS.Repository.Entities
{
    public partial class NamedQuery
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public string Name { get; set; }
        public bool Global { get; set; }
        public string Template { get; set; }
    }
}
