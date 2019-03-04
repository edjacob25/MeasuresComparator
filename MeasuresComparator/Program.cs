using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MeasuresComparator
{
    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    Console.WriteLine($"Options are {o.ClassifiedFile} and {o.ReferenceFile}");

                    if (!File.Exists(o.ClassifiedFile) || !File.Exists(o.ReferenceFile))
                    {
                        Console.WriteLine("One of the files does not exist or cannot be found");
                        return;
                    }
                    try
                    {
                        var clusterized = ReadArffFile(o.ClassifiedFile);
                        var reference = ReadArffFile(o.ReferenceFile);

                        if (clusterized.Headers.Contains("Instance_number".ToLowerInvariant()))
                        {
                            clusterized.EliminateHeader("Instance_number".ToLowerInvariant());
                        }

                        if (!clusterized.Headers.Contains("cluster") || !reference.Headers.Contains("cluster"))
                        {
                            Console.WriteLine("One of the files does not contain the cluster property");
                            return;
                        }

                        if (!clusterized.Headers.SequenceEqual(reference.Headers))
                        {
                            Console.WriteLine($"Datasets do not have the same headers");
                            return;
                        }

                        if (clusterized.Count != reference.Count)
                        {
                            Console.WriteLine($"Datasets are not of the same length, clusterized one is {clusterized.Count} instances " +
                                $"long and reference is {reference.Count} instances long");
                            return;
                        }

                        Console.WriteLine($"The F-Measure is {CalculateFMeasure(reference, clusterized)}");
                    }
                    catch (Exception e)
                    {
                        if (e is ParserException)
                        {
                            Console.WriteLine("Could not parse file");
                        }
                    }
                });
        }

        public class Options
        {
            [Option('c', "classified", Required = true, HelpText = "Path of the classified file to compare")]
            public string ClassifiedFile { get; set; }

            [Option('r', "reference", Required = true, HelpText = "Path of the reference file to compare")]
            public string ReferenceFile { get; set; }

        }

        private static Dataset ReadArffFile(string route)
        {
            using (var reader = new StreamReader(route))
            {
                var headers = new List<string>();
                var name = string.Empty;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf("@data", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }

                    if (line.IndexOf("@RELATION", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var array = line.Split(' ');
                        if (array.Length < 2)
                        {
                            throw new ArgumentOutOfRangeException("fileName", "Failed to parse name");
                        }

                        name = array[1];
                    }
                    else if (line.IndexOf("@attribute", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var split = line.Split(' ');
                        // Process headers here
                        headers.Add(split[1].ToLowerInvariant());
                    }
                }
                var dataset = new Dataset(name, headers);
                int i = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    var currentLine = line;
                    currentLine = line.Replace("{", string.Empty);
                    currentLine = line.Replace("}", string.Empty);
                    var record = new Record(headers, currentLine.Split(','), i);
                    dataset.InsertRecord(record);
                    i++;
                }

                return dataset;
            }

        }

        private static double CalculateFMeasure(Dataset reference, Dataset test)
        {
            var referencePairs = CreatePairs(reference);
            var testPairs = CreatePairs(test);
            var truePositives = referencePairs.Intersect(testPairs, new PairComparator()).Count();
            var falsePositives = testPairs.Count - truePositives;
            var falseNegatives = referencePairs.Count - truePositives;

            var precision = (float)truePositives / (truePositives + falsePositives);
            var recall = (float)truePositives / (truePositives + falseNegatives);

            return 2 * ((precision * recall) / (precision + recall));
        }

        private class PairComparator : EqualityComparer<(int, int)>
        {
            public override bool Equals((int, int) x, (int, int) y)
                => x.Item1 == y.Item1 && x.Item2 == y.Item2;

            public override int GetHashCode((int, int) obj) => (obj.Item1 + obj.Item2).GetHashCode();
        }

        private static List<(int, int)> CreatePairs(Dataset set)
        {
            var partitions = set.Records.GroupBy(e => e.GetValue("cluster"));
            var pairs = new List<(int, int)>();
            foreach (var cluster in partitions)
            {
                var clusterPairs = cluster.DifferentCombinations(2).Select(e => (e.FirstOrDefault().Position, e.Skip(1).FirstOrDefault().Position));
                pairs.AddRange(clusterPairs);
            }
            return pairs;
        }
    }

    public static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> DifferentCombinations<T>(this IEnumerable<T> elements, int k)
        {
            return k == 0 ? new[] { new T[0] } :
              elements.SelectMany((e, i) =>
                elements.Skip(i + 1).DifferentCombinations(k - 1).Select(c => (new[] { e }).Concat(c)));
        }
    }
}
