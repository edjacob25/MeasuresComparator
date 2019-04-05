using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MeasuresComparator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    if (!Console.IsOutputRedirected)
                        Console.WriteLine($"Options are {o.ClassifiedFile} and {o.ReferenceFile}");

                    if (!File.Exists(o.ClassifiedFile) || !File.Exists(o.ReferenceFile))
                    {
                        Console.WriteLine("One of the files does not exist or cannot be found");
                        Environment.ExitCode = 1;
                        return;
                    }

                    try
                    {
                        var clusterized = ReadArffFile(o.ClassifiedFile);
                        var reference = ReadArffFile(o.ReferenceFile);

                        if (clusterized.ContainsHeader("Instance_number".ToLowerInvariant()))
                            clusterized.EliminateHeader("Instance_number".ToLowerInvariant());

                        if (!clusterized.ContainsHeader("cluster") || !reference.ContainsHeader("class") &&
                            !reference.ContainsHeader("cluster"))
                        {
                            Console.WriteLine("The clusterized file does not contain the 'cluster' attribute or " +
                                              "the reference file does not contain a 'class' or 'cluster' property");
                            Environment.ExitCode = 1;
                            return;
                        }

                        if (reference.ContainsHeader("class")) reference.TransformClassToCluster();

                        if (!clusterized.Headers.Select(e => e.Name)
                            .SequenceEqual(reference.Headers.Select(e => e.Name)))
                        {
                            Console.WriteLine("Datasets do not have the same headers");
                            Environment.ExitCode = 1;
                            return;
                        }

                        if (clusterized.Count != reference.Count)
                        {
                            Console.WriteLine(
                                $"Datasets are not of the same length, clusterized one is {clusterized.Count} instances " +
                                $"long and reference is {reference.Count} instances long");
                            Environment.ExitCode = 1;
                            return;
                        }

                        var fMeasure = CalculateFMeasure(reference, clusterized);
                        Console.WriteLine(Console.IsOutputRedirected ? $"{fMeasure}" : $"The F-Measure is {fMeasure}");
                    }
                    catch (Exception e)
                    {
                        if (e is ParserException) Console.WriteLine("Could not parse file");
                    }
                });
        }

        private static Dataset ReadArffFile(string route)
        {
            using (var reader = new StreamReader(route))
            {
                var headers = new List<Header>();
                var name = string.Empty;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf("@data", StringComparison.OrdinalIgnoreCase) >= 0) break;

                    if (line.IndexOf("@RELATION", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var array = line.Split(' ');
                        if (array.Length < 2) throw new Exception("Failed to parse name");

                        name = array[1];
                    }
                    else if (line.IndexOf("@attribute", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var split = line.Split(' ', 3);
                        var header = new Header
                        {
                            Name = split[1].ToLowerInvariant(),
                            Possibilities = split[2].Replace("{", "").Replace("}", "").Split(',').Select(e => e.Trim())
                                .ToList(),
                            Type = "Categorical"
                        };
                        // Process headers here
                        headers.Add(header);
                    }
                }

                var dataset = new Dataset(name, headers);

                var i = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    var currentLine = line.Replace("{", string.Empty);
                    currentLine = currentLine.Replace("}", string.Empty);
                    var record = new Record(headers.Select(e => e.Name).ToList(), currentLine.Split(','), i);
                    dataset.InsertRecord(record);
                    i++;
                }

                return dataset;
            }
        }

        private static double CalculateFMeasure(Dataset reference, Dataset test)
        {
            var watch = new Stopwatch();
            watch.Start();
            var referencePairs = CreatePairs(reference);
            watch.Stop();
            if (!Console.IsOutputRedirected)
            {
                Console.WriteLine($"Create pairs for reference took {watch.ElapsedMilliseconds} milliseconds ");
            }
            watch.Restart();
            watch.Start();
            var testPairs = CreatePairs(test);
            watch.Stop();
            if (!Console.IsOutputRedirected)
            {
                Console.WriteLine($"Create pairs for test took {watch.ElapsedMilliseconds} milliseconds ");
            }

            watch.Restart();
            watch.Start();
            var truePositives = 0;
            var comparer = new PairComparer();
            testPairs.Sort(comparer);
            for (var i = 0; i < referencePairs.Count; i++)
            {
                var index = testPairs.BinarySearch(referencePairs[i], comparer);
                if (index >= 0)
                {
                    truePositives += 1;
                }
            }
            watch.Stop();
            if (!Console.IsOutputRedirected)
            {
                Console.WriteLine($"Calculate true positives manually took {watch.ElapsedMilliseconds} milliseconds ");
            }

            var falsePositives = testPairs.Count - truePositives;
            var falseNegatives = referencePairs.Count - truePositives;

            var precision = (float)truePositives / (truePositives + falsePositives);
            var recall = (float)truePositives / (truePositives + falseNegatives);

            return 2 * (precision * recall / (precision + recall));
        }

        private static void PrintPairs(IEnumerable<(int, int)> pairList)
        {
            foreach (var (item1, item2) in pairList) Console.Write($"({item1},{item2}),");
        }

        private static List<(int, int)> CreatePairs(Dataset set)
        {
            var partitions = set.Records.GroupBy(e => e.GetValue("cluster"));
            var pairs = new List<(int, int)>();
            foreach (var cluster in partitions)
            {
                var clusterPairs = new List<(int, int)>();
                var cl = cluster.ToArray();
                for (int i = 0; i < cluster.Count(); i++)
                {
                    for (int j = i + 1; j < cluster.Count(); j++)
                    {
                        clusterPairs.Add((cl[i].Position, cl[j].Position));
                    }
                }
                pairs.AddRange(clusterPairs);
            }

            return pairs;
        }

        private class Options
        {
            [Option('c', "classified", Required = true, HelpText = "Path of the classified file to compare")]
            public string ClassifiedFile { get; set; }

            [Option('r', "reference", Required = true, HelpText = "Path of the reference file to compare")]
            public string ReferenceFile { get; set; }
        }

        private class PairComparer : IComparer<(int, int)>
        {
            public int Compare((int, int) x, (int, int) y)
            {
                if (x.Item1 < y.Item1)
                {
                    return -1;
                }
                if (x.Item1 > y.Item1)
                {
                    return 1;
                }
                if (x.Item2 < y.Item2)
                {
                    return -1;
                }
                if (x.Item2 > y.Item2)
                {
                    return 1;
                }
                return 0;
            }
        }

        private class PairComparator : EqualityComparer<(int, int)>
        {
            public override bool Equals((int, int) x, (int, int) y)
            {
                return x.Item1 == y.Item1 && x.Item2 == y.Item2;
            }

            public override int GetHashCode((int, int) obj)
            {
                return (obj.Item1 + obj.Item2).GetHashCode();
            }
        }
    }

    public static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> DifferentCombinations<T>(this IEnumerable<T> elements, int k)
        {
            return k == 0
                ? new[] { new T[0] }
                : elements.SelectMany((e, i) =>
                    elements.Skip(i + 1).DifferentCombinations(k - 1).Select(c => new[] { e }.Concat(c)));
        }
    }
}