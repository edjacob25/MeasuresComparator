using System.Collections.Generic;
using System.Linq;

namespace MeasuresComparator
{
    internal class Record
    {
        public Dictionary<string, string> Values { get; }
        public IList<string> Headers { get; }
        public int Position { get; }

        public Record(IList<string> headers, IEnumerable<string> values, int position = 0)
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