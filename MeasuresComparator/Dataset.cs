using System;
using System.Collections.Generic;
using System.Linq;

namespace MeasuresComparator
{
    internal class Dataset
    {
        public IList<Header> Headers { get; private set; }
        public IList<Record> Records { get; }
        public int Count => Records.Count;
        public string Name { get; }

        public Dataset(string name, IList<Header> headers)
        {
            Name = name;
            Headers = headers;
            Records = new List<Record>();
        }

        public void InsertRecord(Record record)
        {
            if (!Headers.Select(e => e.Name).SequenceEqual(record.Headers))
            {
                throw new Exception("Headers do not match");
            }
            Records.Add(record);
        }

        public void EliminateHeader(string name)
        {
            if (!ContainsHeader(name.ToLowerInvariant()))
            {
                throw new Exception("Header does not exist");
            }
            else
            {
                var header = Headers.Single(e => e.Name == name);
                Headers.Remove(header);
                foreach (var record in Records)
                {
                    record.Headers.Remove(name);
                    record.Values.Remove(name);
                }
            }
        }

        public bool ContainsHeader(string name) =>
            Headers.Any(header => header.Name == name.ToLowerInvariant());

        public void TransformClassToCluster()
        {
            if (!ContainsHeader("class"))
            {
                throw new Exception("Header does not exist");
            }
            else
            {
                var header = Headers.Single(e => e.Name == "class");
                header.Name = "cluster";
                var i = 1;
                var equivalences = new List<(string, string)>();
                foreach (var posibility in header.Possibilities)
                {
                    var newName = $"cluster{i}";
                    equivalences.Add((posibility, newName));
                    i++;
                }

                header.Possibilities = equivalences.Select(e => e.Item2).ToList();

                foreach (var record in Records)
                {
                    var index = record.Headers.IndexOf("class");
                    record.Headers[index] = "cluster";
                    var value = record.Values.GetValueOrDefault("class");
                    record.Values.Remove("class");
                    record.Values.Add("cluster", equivalences.Single(e => e.Item1 == value).Item2);
                }
            }
        }
    }
}