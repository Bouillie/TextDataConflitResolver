using System;
using System.IO;
using CommandLine;

namespace TextDataConflictResolver
{
    public class Options
    {
        [Value(0, MetaName = "Base", Required = true, HelpText = "Base file path.")]
        public string Base { get; set; }
        [Value(1, MetaName = "Local", Required = true, HelpText = "Local file path.")]
        public string Local { get; set; }
        [Value(2, MetaName = "Remote", Required = true, HelpText = "Remote file path.")]
        public string Remote { get; set; }
        [Value(3, MetaName = "DestinationPath", Required = false, HelpText = "Destination path.")]
        public string DestinationPath { get; set; }
        [Option('o', Required = false, HelpText = "Output file path. If absent, Local will be overwritten.")]
        public string OutputPath { get; set; }
    }
    
    internal class Program
    {
        public static int Main(string[] args)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            Console.WriteLine(currentDirectory);

            int result = Parser.Default.ParseArguments<Options>(args).MapResult(
                o =>
                {
                    YAMLParser yamlParser = new YAMLParser();
                    string destinationPath = o.DestinationPath == null
                        ? null
                        : currentDirectory + Path.DirectorySeparatorChar + o.DestinationPath;

                    string outputPath = o.OutputPath ?? o.Local;
                    
                    bool success = yamlParser.Parse(o.Base, o.Local, o.Remote, outputPath, destinationPath);

                    if (success)
                        Console.WriteLine("Merge successful.");
                    else
                        Console.WriteLine("Merged with errors.");
                    
                    return success ? 0 : 1;
                },

                errors =>
                {
                    foreach (Error error in errors)
                    {
                        Console.WriteLine(error.ToString());
                    }

                    return -1;
                }
            );

            return result;

        }
    }
}