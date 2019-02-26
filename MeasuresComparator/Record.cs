using System;
using System.Collections.Generic;
using System.Linq;

namespace MeasuresComparator
{
    class Record
    {
        public Dictionary<string, string> Values { get; private set; }
        public IList<String> Headers { get; }

        public Record(IList<string> headers, IList<string> values)
        {
            Headers = headers;
            Values = new Dictionary<string, string>();
            foreach (var pair in headers.Zip(values, (h, v) => new { Header = h, Value = v }))
            {
                Values.TryAdd(pair.Header, pair.Value);
            }
        }

        public string GetValue(string header)
        {
            var value = Values.GetValueOrDefault(header);
            return value;
        }
    }
}
