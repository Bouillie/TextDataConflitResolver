using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ConsoleApplication1.Utils;
using YamlDotNet.RepresentationModel;

namespace ConsoleApplication1
{

    
    
    public class Data
    {
        public SortedListDictionary<int, string> textKeyDictionary = new SortedListDictionary<int, string>();
        public SortedListDictionary<int, int> versionDictionary = new SortedListDictionary<int, int>();
    }

    public class Modifications
    {
        public Dictionary<int, Operation<int, string>> textKeyOperations = new Dictionary<int, Operation<int, string>>();
        public Dictionary<int, Operation<int, int>> versionOperations = new Dictionary<int, Operation<int, int>>();
    }

    public class ModificationResult
    {
        public List<Operation<int, string>> textKeyOperations = new List<Operation<int, string>>();
        public List<Operation<int, int>> versionOperations = new List<Operation<int, int>>();

        public List<Tuple<Operation, Operation>> m_invalidOperations = new List<Tuple<Operation, Operation>>();

        public void Apply(Data data)
        {
            foreach (Operation<int, string> operations in textKeyOperations)
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

            foreach (Operation<int, int> operations in versionOperations)
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

            foreach (Tuple<Operation,Operation> pair in m_invalidOperations)
            {
                a.AppendLine(pair.Item1.ToString());
                b.AppendLine(pair.Item2.ToString());
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

        public bool Parse(string pathSource, string pathA, string pathB)
        {
            YamlStream yamlSource;
            Data sourceData;
            ModificationResult modificationResult;
            
            using (FileStream fs = File.Open(pathSource, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream fsa = File.Open(pathA, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (FileStream fsb = File.Open(pathB, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        StreamReader streamReader = new StreamReader(fs, Encoding.UTF8);
                        StreamReader streamReaderA = new StreamReader(fsa, Encoding.UTF8);
                        StreamReader streamReaderB = new StreamReader(fsb, Encoding.UTF8);
                        
                        yamlSource = new YamlStream();
                        yamlSource.Load(streamReader);
                        sourceData = Parse(yamlSource);

                        YamlStream yamlSourceA = new YamlStream();
                        yamlSourceA.Load(streamReaderA);
                        Data aData = Parse(yamlSourceA);
                        YamlStream yamlSourceB = new YamlStream();
                        yamlSourceB.Load(streamReaderB);
                        Data bData = Parse(yamlSourceB);
            
                        Modifications aModifications = GenerateDiffs(sourceData, aData);
                        Modifications bModifications = GenerateDiffs(sourceData, bData);
                        
                        modificationResult = ComputeResults(aModifications, bModifications);
                        modificationResult.Apply(sourceData);
                        
                        WriteDocument(yamlSource.Documents[0], sourceData);
                    }
                }
            }
            
//            File.Delete(pathA);
            
            using (FileStream fs2 = File.Open(pathA + "_RESULT", FileMode.OpenOrCreate, FileAccess.Write,
                FileShare.None))
            {
                bool success = true;
                StreamWriter output = new StreamWriter(fs2, Encoding.UTF8);
                output.WriteLine("%YAML 1.1");
                output.WriteLine("%TAG !u! tag:unity3d.com,2011:");
                output.Write("--- !u!114 ");
                yamlSource.Save(output, false);
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
            foreach (KeyValuePair<int,Operation<int,string>> operation in a.textKeyOperations)
            {
                if (b.textKeyOperations.TryGetValue(operation.Key, out Operation<int, string> op2))
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(operation.Value, op2));
                }
                else
                {
                    result.textKeyOperations.Add(operation.Value);
                }
            }
            foreach (KeyValuePair<int,Operation<int,int>> operation in a.versionOperations)
            {
                if (b.versionOperations.TryGetValue(operation.Key, out Operation<int, int> op2))
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(operation.Value, op2));
                }
                else
                {
                    result.versionOperations.Add(operation.Value);
                }
            }
            foreach (KeyValuePair<int,Operation<int,string>> operation in b.textKeyOperations)
            {
                if (!a.textKeyOperations.TryGetValue(operation.Key, out Operation<int, string> op2))
                {
                    result.textKeyOperations.Add(operation.Value);
                }
            }
            foreach (KeyValuePair<int,Operation<int,int>> operation in b.versionOperations)
            {
                if (!a.versionOperations.TryGetValue(operation.Key, out Operation<int, int> op2))
                {
                    result.versionOperations.Add(operation.Value);
                }
            }

            return result;
        }
        
        public Modifications GenerateDiffs(Data source, Data modif)
        {
            Modifications modifications = new Modifications();
            foreach (KeyValuePair<int,string> pair in source.textKeyDictionary)
            {
                if (modif.textKeyDictionary.TryGetValue(pair.Key, out string value))
                {
                    if (!string.Equals(value, pair.Value))
                    {
                        modifications.textKeyOperations.Add(pair.Key, new Operation<int, string>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.textKeyOperations.Add(pair.Key, new Operation<int, string>(pair.Key, null, OperationType.REMOVAL));
                }
            }
            foreach (KeyValuePair<int,string> pair in modif.textKeyDictionary)
            {
                if (!source.textKeyDictionary.Contains(pair.Key))
                {
                    modifications.textKeyOperations.Add(pair.Key, new Operation<int, string>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }
            foreach (KeyValuePair<int,int> pair in source.versionDictionary)
            {
                if (modif.versionDictionary.TryGetValue(pair.Key, out int value))
                {
                    if (value != pair.Value)
                    {
                        modifications.versionOperations.Add(pair.Key, new Operation<int, int>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.versionOperations.Add(pair.Key, new Operation<int, int>(pair.Key, 0, OperationType.REMOVAL));
                }
            }
            foreach (KeyValuePair<int,int> pair in modif.versionDictionary)
            {
                if (!source.versionDictionary.Contains(pair.Key))
                {
                    modifications.versionOperations.Add(pair.Key, new Operation<int, int>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }

            return modifications;
        }
        
        public Data Parse(YamlStream yaml)
        {
            return ParseDocument(yaml.Documents[0]);
        }

        private YamlMappingNode GetTextKeysNode(YamlMappingNode parent, params string[] keys)
        {
            if (parent.Children.TryGetValue(new YamlScalarNode("m_textKeyDictionary"), out YamlNode node))
            {
                return (YamlMappingNode) node;
            }
            
            if (parent.Children.TryGetValue(new YamlScalarNode("m_dataDictionary"), out YamlNode node2))
            {
                return (YamlMappingNode) node2;
            }

            return null;
        }    
        
        private YamlMappingNode GetVersionNode(YamlMappingNode parent)
        {
            if (parent.Children.TryGetValue(new YamlScalarNode("m_versionDictionary"), out YamlNode node))
            {
                return (YamlMappingNode) node;
            }
            
            if (parent.Children.TryGetValue(new YamlScalarNode("m_textVersionDictionary"), out YamlNode node2))
            {
                return (YamlMappingNode) node2;
            }

            return null;
        }
        
        private void WriteDocument(YamlDocument document, Data data)
        {
            YamlMappingNode mapping = (YamlMappingNode) document.RootNode;
            YamlMappingNode monoBehaviourNode = (YamlMappingNode) mapping.Children[new YamlScalarNode("MonoBehaviour")];
            YamlMappingNode textKeyDictionary = GetTextKeysNode(monoBehaviourNode);
            YamlMappingNode versionDictionary = GetVersionNode(monoBehaviourNode);
            WriteTextKeyDictionary(textKeyDictionary, data);
            WriteVersionDictionary(versionDictionary, data);
        }
        
        private Data ParseDocument(YamlDocument document)
        {
            YamlMappingNode mapping = (YamlMappingNode) document.RootNode;
            Data data = new Data();

            YamlMappingNode monoBehaviourNode = (YamlMappingNode) mapping.Children[new YamlScalarNode("MonoBehaviour")];
            YamlMappingNode textKeyDictionary = GetTextKeysNode(monoBehaviourNode);
            ParseTextKeyDictionary(textKeyDictionary, data);
            YamlMappingNode versionDictionary = GetVersionNode(monoBehaviourNode);
            ParseVersionDictionary(versionDictionary, data);

            return data;
        }

        private void ParseVersionDictionary(YamlMappingNode mapping, Data data)
        {
            YamlScalarNode keysNode = (YamlScalarNode) mapping.Children[new YamlScalarNode("m_keys")];
            string keysValue = keysNode.Value;
            int[] keys = ParseSerializedIntArray(keysValue);
            
            YamlScalarNode valuesNode = (YamlScalarNode) mapping.Children[new YamlScalarNode("m_values")];
            string valuesValue = valuesNode.Value;
            int[] values = ParseSerializedIntArray(valuesValue);
            
            for (int i = 0, count = keys.Length; i < count; ++i) 
            {
                data.versionDictionary.Add(keys[i], values[i]);
            }
        }
        
        private void WriteVersionDictionary(YamlMappingNode mapping, Data data)
        {
            StringBuilder keysBuilder = new StringBuilder();
            StringBuilder valuesBuilder = new StringBuilder();

            foreach (KeyValuePair<int, int> pair in data.versionDictionary)
            {
                keysBuilder.Append(ReverseHexString(pair.Key.ToString("x8")));
                valuesBuilder.Append(ReverseHexString(pair.Value.ToString("x8")));
            }
            
            mapping.Children[new YamlScalarNode("m_keys")] = new YamlScalarNode(keysBuilder.ToString());
            string value = valuesBuilder.ToString();
            mapping.Children[new YamlScalarNode("m_values")] = new YamlScalarNode(value);
        }
        
        private void ParseTextKeyDictionary(YamlMappingNode mapping, Data data)
        {
            YamlScalarNode keysNode = (YamlScalarNode) mapping.Children[new YamlScalarNode("m_keys")];
            string keysValue = keysNode.Value;
            int[] keys = ParseSerializedIntArray(keysValue);
            
            YamlSequenceNode values = (YamlSequenceNode) mapping.Children[new YamlScalarNode("m_values")];
            int index = 0;
            foreach (YamlScalarNode value in values)
            {
                data.textKeyDictionary.Add(keys[index++], value.Value);
            }
        }
        
        private void WriteTextKeyDictionary(YamlMappingNode mapping, Data data)
        {
            YamlSequenceNode valuesSequence = new YamlSequenceNode();
            
            StringBuilder keysBuilder = new StringBuilder();

            foreach (KeyValuePair<int, string> pair in data.textKeyDictionary)
            {
                keysBuilder.Append(ReverseHexString(pair.Key.ToString("x8")));
                valuesSequence.Add(new YamlScalarNode(pair.Value));
            }
            
            mapping.Children[new YamlScalarNode("m_keys")] = new YamlScalarNode(keysBuilder.ToString());
            mapping.Children[new YamlScalarNode("m_values")] = valuesSequence;
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