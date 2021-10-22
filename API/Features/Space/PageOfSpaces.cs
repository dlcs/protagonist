using System.Collections.Generic;

namespace API.Features.Space
{
    public class PageOfSpaces
    {
        public List<DLCS.Repository.Entities.Space> Spaces { get; set; }
        public int Page { get; set; }
        public int Total { get; set; }
    }
}