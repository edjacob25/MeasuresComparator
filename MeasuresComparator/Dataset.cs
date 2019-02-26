using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MeasuresComparator
{
    class Dataset
    {
        public IList<string> Headers { get; private set; }
        public IList<Record> Records { get; }
        public int Count => Records.Count;
        public string Name { get; }

        public Dataset(string name, IList<string> headers)
        {
            Name = name;
            Headers = headers;
            Records = new List<Record>();
        }

        public void InsertRecord(Record record)
        {
            if (!Headers.SequenceEqual(record.Headers))
            {
                throw new Exception("Headers do not match");
            }
            Records.Add(record);
        }

        public void EliminateHeader(string name)
        {
            if (!Headers.Contains(name.ToLowerInvariant()))
            {
                throw new Exception("Header does not exist");
            }
            else
            {
                Headers.Remove(name);
                foreach (var record in Records)
                {
                    record.Headers.Remove(name);
                    record.Values.Remove(name);
                }
            }
        }
    }
}
