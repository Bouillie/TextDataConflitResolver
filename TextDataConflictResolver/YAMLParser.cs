using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TextDataConflictResolver.SimpleParser;
using TextDataConflictResolver.Utils;

namespace TextDataConflictResolver
{
    
    public class Data
    {
        public SortedListDictionary<int, Line> textKeyDictionary = new SortedListDictionary<int, Line>();
        public SortedListDictionary<int, Line> versionDictionary = new SortedListDictionary<int, Line>();
    }

    public class Modifications
    {
        public Dictionary<int, Operation<int, Line>> textKeyOperations = new Dictionary<int, Operation<int, Line>>();
        public Dictionary<int, Operation<int, Line>> versionOperations = new Dictionary<int, Operation<int, Line>>();
    }

    public class ModificationResult
    {
        public List<Operation<int, Line>> textKeyOperations = new List<Operation<int, Line>>();
        public List<Operation<int, Line>> versionOperations = new List<Operation<int, Line>>();

        public List<Tuple<Operation, Operation>> m_invalidOperations = new List<Tuple<Operation, Operation>>();

        public void Apply(Data data)
        {
            foreach (Operation<int, Line> operations in textKeyOperations)
            {
                switch (operations.OperationType)
                {
                    case OperationType.ADDITION:
                        data.textKeyDictionary.Add(operations.Key, operations.Value);
                        break;
                    case OperationType.MODIFICATION:
                        data.textKeyDictionary[operations.Key] = operations.Value;
                        break;
                    case OperationType.REMOVAL:
                        data.textKeyDictionary.Remove(operations.Key);
                        break;
                }
            }

            foreach (Operation<int, Line> operations in versionOperations)
            {
                switch (operations.OperationType)
                {
                    case OperationType.ADDITION:
                        data.versionDictionary.Add(operations.Key, operations.Value);
                        break;
                    case OperationType.MODIFICATION:
                        data.versionDictionary[operations.Key] = operations.Value;
                        break;
                    case OperationType.REMOVAL:
                        data.versionDictionary.Remove(operations.Key);
                        break;
                }
            }
        }

        public string Errors()
        {
            if (m_invalidOperations.Count == 0)
                return string.Empty;
            
            StringBuilder a = new StringBuilder();
            StringBuilder b = new StringBuilder();

            for (int index = 0; index < m_invalidOperations.Count; index++)
            {
                if (index != 0)
                {
                    a.Append("\n");
                    b.Append("\n");
                }
                Tuple<Operation, Operation> pair = m_invalidOperations[index];
                a.Append(pair.Item1);
                b.Append(pair.Item2);
            }

            StringBuilder result = new StringBuilder();
            result.AppendLine("<<<<<<< HEAD");
            result.AppendLine(a.ToString());
            result.AppendLine("=======");
            result.AppendLine(b.ToString());
            result.AppendLine(">>>>>>> ");
            return result.ToString();
        }
    }
    
    public enum OperationType
    {
        ADDITION,
        MODIFICATION,
        REMOVAL,
    }

    public abstract class Operation {}
    
    public class Operation<T, U>: Operation
    {
        public Operation(T key, U value, OperationType operationType)
        {
            Key = key;
            Value = value;
            OperationType = operationType;
        }

        public OperationType OperationType { get; }
        public T Key { get; }
        public U Value { get; }

        public override string ToString()
        {
            return $"{OperationType}: {Key} => {Value}";
        }
    }
  
    public class YAMLParser
    {

        public bool Parse(string pathSource, string pathA, string pathB, string destinationPath)
        {
            Document yamlSource;
            Data sourceData;
            ModificationResult modificationResult;

            if (destinationPath != null)
            {
                string fileName = Path.GetFileNameWithoutExtension(pathSource);
                string directoryName = Path.GetDirectoryName(destinationPath) ?? "";
                string extension = Path.GetExtension(pathSource);
                
                string now = DateTime.Now.ToString("s").Replace(':', '-');
                string pathABackup = Path.Combine(directoryName,  $"{fileName}_LOCAL_{now}{extension}");
                string pathBBackup = Path.Combine(directoryName,  $"{fileName}_REMOTE_{now}{extension}");
                string pathSourceBackup = Path.Combine(directoryName,  $"{fileName}_BASE_{now}{extension}");
                
                File.Copy(pathA, pathABackup);
                File.Copy(pathB, pathBBackup);
                File.Copy(pathSource, pathSourceBackup);
            }
            
            using (FileStream fs = File.Open(pathSource, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsa = File.Open(pathA, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (FileStream fsb = File.Open(pathB, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        StreamReader streamReader = new StreamReader(fs, Encoding.UTF8);
                        StreamReader streamReaderA = new StreamReader(fsa, Encoding.UTF8);
                        StreamReader streamReaderB = new StreamReader(fsb, Encoding.UTF8);

                        string[] textNames = {
                            "m_textKeyDictionary",
                            "m_dataDictionary",
                        };
                        string[] versionsNames = {
                            "m_editorInfoDictionary",
                        };

                        yamlSource = new Document(textNames, versionsNames);
                        yamlSource.Parse(streamReader);
                        sourceData = ParseDocument(yamlSource);

                        Document yamlSourceA = new Document(textNames, versionsNames);
                        yamlSourceA.Parse(streamReaderA);
                        Data aData = ParseDocument(yamlSourceA);
                        Document yamlSourceB = new Document(textNames, versionsNames);
                        yamlSourceB.Parse(streamReaderB);
                        Data bData = ParseDocument(yamlSourceB);
            
                        Modifications aModifications = GenerateDiffs(sourceData, aData);
                        Modifications bModifications = GenerateDiffs(sourceData, bData);
                        
                        modificationResult = ComputeResults(aModifications, bModifications);
                        modificationResult.Apply(sourceData);
                        
                        WriteDocument(yamlSource, sourceData);
                    }
                }
            }
            
            File.Delete(pathA);
            
            using (FileStream fs2 = File.Open(pathA, FileMode.OpenOrCreate, FileAccess.Write,
                FileShare.None))
            {
                bool success = true;
                StreamWriter output = new StreamWriter(fs2, new UTF8Encoding(false)) { NewLine = "\n" };
                yamlSource.Save(output);
                string errors = modificationResult.Errors();
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    output.WriteLine(errors);
                    success = false;
                }
                output.Flush();
                
                return success;
            }
        }

        
        public ModificationResult ComputeResults(Modifications a, Modifications b)
        {
            ModificationResult result = new ModificationResult();
            foreach (KeyValuePair<int,Operation<int,Line>> operation in a.textKeyOperations)
            {
                if (b.textKeyOperations.TryGetValue(operation.Key, out Operation<int, Line> op2))
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(operation.Value, op2));
                }
                else
                {
                    result.textKeyOperations.Add(operation.Value);
                }
            }
            foreach (KeyValuePair<int,Operation<int,Line>> operation in a.versionOperations)
            {
                if (b.versionOperations.TryGetValue(operation.Key, out Operation<int, Line> op2))
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(operation.Value, op2));
                }
                else
                {
                    result.versionOperations.Add(operation.Value);
                }
            }
            foreach (KeyValuePair<int,Operation<int,Line>> operation in b.textKeyOperations)
            {
                if (!a.textKeyOperations.TryGetValue(operation.Key, out Operation<int, Line> op2))
                {
                    result.textKeyOperations.Add(operation.Value);
                }
            }
            foreach (KeyValuePair<int,Operation<int,Line>> operation in b.versionOperations)
            {
                if (!a.versionOperations.TryGetValue(operation.Key, out Operation<int, Line> op2))
                {
                    result.versionOperations.Add(operation.Value);
                }
            }

            return result;
        }
        
        public Modifications GenerateDiffs(Data source, Data modif)
        {
            Modifications modifications = new Modifications();
            foreach (KeyValuePair<int,Line> pair in source.textKeyDictionary)
            {
                if (modif.textKeyDictionary.TryGetValue(pair.Key, out Line value))
                {
                    if (!string.Equals(value.ScalarValue, pair.Value.ScalarValue))
                    {
                        modifications.textKeyOperations.Add(pair.Key, new Operation<int, Line>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.textKeyOperations.Add(pair.Key, new Operation<int, Line>(pair.Key, null, OperationType.REMOVAL));
                }
            }
            foreach (KeyValuePair<int,Line> pair in modif.textKeyDictionary)
            {
                if (!source.textKeyDictionary.Contains(pair.Key))
                {
                    modifications.textKeyOperations.Add(pair.Key, new Operation<int, Line>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }
            foreach (KeyValuePair<int,Line> pair in source.versionDictionary)
            {
                if (modif.versionDictionary.TryGetValue(pair.Key, out Line value))
                {
                    if (!string.Equals(value.ScalarValue, pair.Value.ScalarValue))
                    {
                        modifications.versionOperations.Add(pair.Key, new Operation<int, Line>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.versionOperations.Add(pair.Key, new Operation<int, Line>(pair.Key, null, OperationType.REMOVAL));
                }
            }
            foreach (KeyValuePair<int,Line> pair in modif.versionDictionary)
            {
                if (!source.versionDictionary.Contains(pair.Key))
                {
                    modifications.versionOperations.Add(pair.Key, new Operation<int, Line>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }

            return modifications;
        }
        
        
        private void WriteDocument(Document document, Data data)
        {
            WriteTextKeyDictionary(document, data);
            WriteVersionDictionary(document, data);
        }
        
        private Data ParseDocument(Document document)
        {
            Data data = new Data();
            ParseTextKeyDictionary(document, data);
            ParseVersionDictionary(document, data);

            return data;
        }

        private void ParseVersionDictionary(Document document, Data data)
        {
            int[] keys = ParseSerializedIntArray(document.VersionDictionaryKeys);
            int index = 0;
            foreach (Line value in document.VersionDictionaryValues)
            {
                data.versionDictionary.Add(keys[index++], value);
            }
        }
        
        private void WriteVersionDictionary(Document document, Data data)
        {
            List<Line> lines = new List<Line>();
            StringBuilder keysBuilder = new StringBuilder();

            foreach (KeyValuePair<int, Line> pair in data.versionDictionary)
            {
                keysBuilder.Append(ReverseHexString(pair.Key.ToString("x8")));
                lines.Add(pair.Value);
            }

            document.VersionDictionaryKeys = keysBuilder.ToString();
            document.VersionDictionaryValues = lines;
        }
        
        private void ParseTextKeyDictionary(Document document, Data data)
        {
            int[] keys = ParseSerializedIntArray(document.TextDictionaryKeys);
            int index = 0;
            foreach (Line value in document.TextDictionaryValues)
            {
                data.textKeyDictionary.Add(keys[index++], value);
            }
        }
        
        private void WriteTextKeyDictionary(Document document, Data data)
        {
            List<Line> lines = new List<Line>();
            StringBuilder keysBuilder = new StringBuilder();

            foreach (KeyValuePair<int, Line> pair in data.textKeyDictionary)
            {
                keysBuilder.Append(ReverseHexString(pair.Key.ToString("x8")));
                lines.Add(pair.Value);
            }

            document.TextDictionaryKeys = keysBuilder.ToString();
            document.TextDictionaryValues = lines;
        }

        private static int[] ParseSerializedIntArray(string serializedIntArray)
        {
            int count = serializedIntArray.Length / 8;
            int[] values = new int[count];
            for (int i = 0; i < count; ++i)
            {
                string substring = ReverseHexString(serializedIntArray.Substring(i*8, 8));
                values[i] = int.Parse(substring, NumberStyles.HexNumber);
            }

            return values;
        }

        
        private static StringBuilder stringBuilder = new StringBuilder();
        private static string ReverseHexString(string s)
        {
            stringBuilder.Clear();
            for (int i = s.Length - 2; i >= 0; i -= 2)
            {
                stringBuilder.Append(s[i]).Append(s[i + 1]);
            }

            return stringBuilder.ToString();
        }
    }
}