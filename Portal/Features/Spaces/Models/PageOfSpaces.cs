using System.Collections.Generic;
using DLCS.Repository.Entities;

namespace Portal.Features.Spaces.Models
{
    public class PageOfSpaces
    {
        public IEnumerable<Space> Spaces { get; set; }
        public int Page { get; set; }
        public int Total { get; set; }
    }
}