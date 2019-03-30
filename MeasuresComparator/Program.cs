using CommandLine;
using System;
using System.Collections.Generic;
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
                    {
                        Console.WriteLine($"Options are {o.ClassifiedFile} and {o.ReferenceFile}");
                    }

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
                        {
                            clusterized.EliminateHeader("Instance_number".ToLowerInvariant());
                        }

                        if (!clusterized.ContainsHeader("cluster") || (!reference.ContainsHeader("class") && !reference.ContainsHeader("cluster")))
                        {
                            Console.WriteLine("The clusterized file does not contain the 'cluster' attribute or " +
                                "the reference file does not contain a 'class' or 'cluster' property");
                            Environment.ExitCode = 1;
                            return;
                        }

                        if (reference.ContainsHeader("class"))
                        {
                            reference.TransformClassToCluster();
                        }

                        if (!clusterized.Headers.Select(e => e.Name).SequenceEqual(reference.Headers.Select(e => e.Name)))
                        {
                            Console.WriteLine($"Datasets do not have the same headers");
                            Environment.ExitCode = 1;
                            return;
                        }

                        if (clusterized.Count != reference.Count)
                        {
                            Console.WriteLine($"Datasets are not of the same length, clusterized one is {clusterized.Count} instances " +
                                $"long and reference is {reference.Count} instances long");
                            Environment.ExitCode = 1;
                            return;
                        }

                        var fMeasure = CalculateFMeasure(reference, clusterized);
                        if (Console.IsOutputRedirected)
                        {
                            Console.WriteLine($"{fMeasure}");
                        }
                        else
                        {
                            Console.WriteLine($"The F-Measure is {fMeasure}");
                        }
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
                var headers = new List<Header>();
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
                        var split = line.Split(' ', 3);
                        var header = new Header()
                        {
                            Name = split[1].ToLowerInvariant(),
                            Possibilities = split[2].Replace("{", "").Replace("}", "").Split(',').Select(e => e.Trim()).ToList(),
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
                    var currentLine = line;
                    currentLine = line.Replace("{", string.Empty);
                    currentLine = line.Replace("}", string.Empty);
                    var record = new Record(headers.Select(e => e.Name).ToList(), currentLine.Split(','), i);
                    dataset.InsertRecord(record);
                    i++;
                }

                return dataset;
            }
        }

        private static double CalculateFMeasure(Dataset reference, Dataset test)
        {
            var referencePairs = CreatePairs(reference);
            //PrintPairs(referencePairs);
            var testPairs = CreatePairs(test);
            //PrintPairs(testPairs);
            var truePositives = referencePairs.Intersect(testPairs, new PairComparator()).Count();
            var falsePositives = testPairs.Count - truePositives;
            var falseNegatives = referencePairs.Count - truePositives;

            var precision = (float)truePositives / (truePositives + falsePositives);
            var recall = (float)truePositives / (truePositives + falseNegatives);

            return 2 * ((precision * recall) / (precision + recall));
        }

        private static void PrintPairs(List<(int, int)> pairList)
        {
            foreach (var item in pairList)
            {
                Console.Write($"({item.Item1},{item.Item2}),");
            }
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
                var clusterPairs = cluster.DifferentCombinations(2)
                    .Select(e => (e.FirstOrDefault().Position, e.Skip(1).FirstOrDefault().Position));
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