using System;
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
        
        [Option('b', "backupDirectory", HelpText = "Enable file backups.")]
        public string BackupDirectory { get; set; }
    }
    
    internal class Program
    {
        public static int Main(string[] args)
        {
            int result = Parser.Default.ParseArguments<Options>(args).MapResult(
                o =>
                {
                    YAMLParser yamlParser = new YAMLParser();
                    bool success = yamlParser.Parse(o.Base, o.Local, o.Remote, o.BackupDirectory);

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