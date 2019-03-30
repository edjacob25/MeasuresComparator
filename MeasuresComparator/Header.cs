using System.Collections.Generic;

namespace MeasuresComparator
{
    internal class Header
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public IList<string> Possibilities { get; set; }
    }
}