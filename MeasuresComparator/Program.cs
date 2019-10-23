using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommandLine;

namespace MeasuresComparator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(GetMeasure);
        }

        private static void GetMeasure(Options o)
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

                if (!clusterized.ContainsHeader("cluster") || !reference.ContainsHeader("class") &&
                    !reference.ContainsHeader("cluster"))
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

                double measure = o switch
                {
                    var l when l.Rand => CalculateRandIndex(reference, clusterized),
                    var l when l.AdjustedRand => CalculateAdjustedRandIndex(reference, clusterized),
                    _ => CalculateFMeasure(reference, clusterized)
                };

                Console.WriteLine(Console.IsOutputRedirected ? $"{measure}" : $"The Measure is {measure}");
            }
            catch (Exception e)
            {
                Environment.ExitCode = 1;
                if (e is ParserException)
                {
                    Console.WriteLine("Could not parse file");
                }
                else
                {
                    Console.WriteLine(e);
                }
            }
        }

        private static Dataset ReadArffFile(string route)
        {
            using var reader = new StreamReader(route);
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
                        throw new Exception("Failed to parse name");
                    }

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
                if (currentLine.Split(',').Length < headers.Count)
                {
                    continue;
                }

                var record = new Record(headers.Select(e => e.Name).ToList(), currentLine.Split(','), i);
                dataset.InsertRecord(record);
                i++;
            }

            return dataset;
        }


        private static ClusteringResult GetClusteringResult(Dataset reference, Dataset test)
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
            foreach (var t in referencePairs)
            {
                var index = testPairs.BinarySearch(t, comparer);
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

            var totalPairs = ((reference.Count - 1) * reference.Count) / 2;
            var falsePositives = testPairs.Count - truePositives;
            var falseNegatives = referencePairs.Count - truePositives;
            return new ClusteringResult
            {
                TruePositives = truePositives,
                FalsePositives = falsePositives,
                FalseNegatives = falseNegatives,
                TrueNegatives = totalPairs - truePositives - falsePositives - falseNegatives
            };
        }

        private static double CalculateFMeasure(Dataset reference, Dataset test)
        {
            var results = GetClusteringResult(reference, test);
            var precision = (float) results.TruePositives / (results.TruePositives + results.FalsePositives);
            var recall = (float) results.TruePositives / (results.TruePositives + results.FalseNegatives);
            return 2 * (precision * recall / (precision + recall));
        }

        private static double CalculateRandIndex(Dataset reference, Dataset test)
        {
            var results = GetClusteringResult(reference, test);
            return (float) (results.TruePositives + results.TrueNegatives) /
                   (results.TruePositives + results.TrueNegatives + results.FalseNegatives + results.FalsePositives);
        }

        private static double CalculateAdjustedRandIndex(Dataset reference, Dataset test)
        {
            var numOfClusters = reference.NumOfClusters;
            // Console.WriteLine($"Num of clusters is {reference.NumOfClusters}");
            var contingencyTable = new int[numOfClusters, numOfClusters];
            for (var i = 0; i < reference.Count; i++)
            {
                var refIndex = int.Parse(reference.Records[i].GetValue("cluster").Replace("cluster", "")) - 1;
                var testIndex = int.Parse(test.Records[i].GetValue("cluster").Replace("cluster", "")) - 1;
                contingencyTable[refIndex, testIndex] += 1;
            }

            var totalSum = 0;
            for (var i = 0; i < numOfClusters; i++)
            {
                for (var j = 0; j < numOfClusters; j++)
                {
                    totalSum += GetN2(contingencyTable[i, j]);
                    // Console.Write($"{contingencyTable[i, j]} ");
                }
                // Console.Write("\n");
            }

            var rowSums = new int[numOfClusters];
            var colsSums = new int[numOfClusters];
            foreach (var i in Enumerable.Range(0, numOfClusters))
            {
                rowSums[i] = contingencyTable.GetRow(i).Sum();
                colsSums[i] = contingencyTable.GetCol(i).Sum();
            }

            var totalRowSum = rowSums.Select(GetN2).Sum();
            var totalColSum = colsSums.Select(GetN2).Sum();

            var less = (double) totalRowSum * totalColSum / GetN2(reference.Count);

            return (totalSum - less ) /
                   (0.5 * (totalColSum + totalRowSum) - less);

            int GetN2(int a) => (a * (a -1)) / 2;
        }

        private static void PrintPairs(IEnumerable<(int, int)> pairList)
        {
            foreach (var (item1, item2) in pairList)
            {
                Console.Write($"({item1},{item2}),");
            }
        }

        // Create pairs of items in each cluster
        private static List<(int, int)> CreatePairs(Dataset set)
        {
            var partitions = set.Records.GroupBy(e => e.GetValue("cluster"));
            var pairs = new List<(int, int)>();
            foreach (var cluster in partitions)
            {
                var clusterPairs = new List<(int, int)>();
                var cl = cluster.ToArray();
                for (var i = 0; i < cluster.Count(); i++)
                {
                    for (var j = i + 1; j < cluster.Count(); j++)
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

            [Option("rand", HelpText = "Use Rand Index instead of F-Measure")]
            public bool Rand { get; set; }

            [Option("adjusted-rand", HelpText = "Use Adjusted Rand Index instead of F-Measure")]
            public bool AdjustedRand { get; set; }
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

                return x.Item2 > y.Item2 ? 1 : 0;
            }
        }
    }


}