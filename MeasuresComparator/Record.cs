using System.Collections.Generic;
using System.Linq;

namespace MeasuresComparator
{
    internal class Record
    {
        public Dictionary<string, string> Values { get; private set; }
        public IList<string> Headers { get; private set; }
        public int Position { get; set; }

        public Record(IList<string> headers, IList<string> values)
        {
            Headers = headers;
            Values = new Dictionary<string, string>();
            foreach (var pair in headers.Zip(values, (h, v) => new { Header = h, Value = v }))
            {
                Values.TryAdd(pair.Header, pair.Value);
            }
        }

        public Record(IList<string> headers, IList<string> values, int position)
        {
            Headers = headers;
            Values = new Dictionary<string, string>();
            foreach (var pair in headers.Zip(values, (h, v) => new { Header = h, Value = v }))
            {
                Values.TryAdd(pair.Header, pair.Value);
            }
            Position = position;
        }

        public string GetValue(string header)
        {
            var value = Values.GetValueOrDefault(header);
            return value;
        }
    }
}