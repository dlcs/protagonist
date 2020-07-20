using System;
using System.Collections.Generic;
using System.Text;

namespace IIIF.Presentation.Strings
{
    public class LabelValuePair
    {
        private LanguageMap valueMap;
        private LanguageMap labelMap;
        public LabelValuePair(string language, string label, string value)
        {

        }

        public LabelValuePair(LanguageMap label, LanguageMap value)
        {

        }

        public LanguageMap Label { get => labelMap; set => labelMap = value; }
        public LanguageMap Value { get => valueMap; set => valueMap = value; }
    }

}
