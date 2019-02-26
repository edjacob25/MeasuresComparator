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
                        var classified = ReadArffFile(o.ClassifiedFile);
                        var referenced = ReadArffFile(o.ReferenceFile);

                        if (classified.Headers.Contains("Instance_number".ToLowerInvariant()))
                        {
                            classified.EliminateHeader("Instance_number".ToLowerInvariant());
                        }

                        if (!classified.Headers.Contains("cluster") || !referenced.Headers.Contains("cluster"))
                        {
                            Console.WriteLine("One of the files does not contain the cluster property");
                            return;
                        }

                        if (!classified.Headers.SequenceEqual(referenced.Headers))
                        {
                            Console.WriteLine($"Datasets do not have the same headers");
                            return;
                        }

                        if (classified.Count != referenced.Count)
                        {
                            Console.WriteLine($"Datasets are not of the same length, classified one is {classified.Count} instances " +
                                $"long and reference is {referenced.Count} instances long");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ParserException)
                        {
                            Console.WriteLine("Could not parse file");
                        }
                    }



                    //foreach (var document in data.Records)
                    //{
                    //    var val = document.GetValue("doors");
                    //    Console.WriteLine($"Door is {val}");
                    //}
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

                while ((line = reader.ReadLine()) != null)
                {
                    var currentLine = line;
                    currentLine = line.Replace("{", string.Empty);
                    currentLine = line.Replace("}", string.Empty);
                    var record = new Record(headers, currentLine.Split(','));
                    dataset.InsertRecord(record);
                }

                return dataset;
            }

        }

        private static double CalculateFMeasure(Dataset reference, Dataset test)
        {
            var truePositives = 0;
            var type1Errors = 0;
            var type2Errors = 0;
            var trueNegatives = 0;

            var referencePartitions = reference.Records.GroupBy(e => e.GetValue("cluster"));
            var referencePairs = referencePartitions
                .Select(e => e);

        }
    }

}
