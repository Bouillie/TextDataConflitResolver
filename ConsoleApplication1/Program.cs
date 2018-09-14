using System;
using System.IO;
using System.Net;
using System.Text;

namespace ConsoleApplication1
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Missing parameters. Expected : {BASE} {LOCAL} {REMOTE}");
                return 1;
            }
            
            string path = args[0];
            string pathA = args[1];
            string pathB = args[2];
            string backupPath = pathA + ".BACKUP";

            // Backup
            File.Copy(pathA, backupPath, true);
            
            YAMLParser yamlParser = new YAMLParser();
            bool success = yamlParser.Parse(path, pathA, pathB);

            if (success)
                Console.WriteLine("Merge successful.");
            else
                Console.WriteLine("Merged with errors.");
            
            return success ? 0 : 1;
        }
    }
}