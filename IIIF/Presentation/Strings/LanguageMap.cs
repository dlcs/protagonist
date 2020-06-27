using System;
using System.Collections.Generic;
using System.Text;

namespace IIIF.Presentation.Strings
{
    public class LanguageMap : Dictionary<string, List<string>>
    {
        public LanguageMap() : base() { }

        // is this a dict?
        public LanguageMap(string language, string singleValue) : base()
        {
            this[language] = new List<string> { singleValue };
        }
    }
}
