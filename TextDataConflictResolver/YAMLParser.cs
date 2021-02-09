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

        public SortedListDictionary<string, (Line, Line)> textCollectionDictionary =
            new SortedListDictionary<string, (Line, Line)>();
    }

    public class Modifications
    {
        public Dictionary<int, Operation<int, Line>> textKeyOperations = new Dictionary<int, Operation<int, Line>>();
        public Dictionary<int, Operation<int, Line>> versionOperations = new Dictionary<int, Operation<int, Line>>();

        public Dictionary<string, Operation<string, (Line, Line)?>> textCollectionOperations =
            new Dictionary<string, Operation<string, (Line, Line)?>>();
    }

    public class ModificationResult
    {
        public List<Operation<int, Line>> textKeyOperations = new List<Operation<int, Line>>();
        public List<Operation<int, Line>> versionOperations = new List<Operation<int, Line>>();

        public List<Operation<string, (Line, Line)?>> textCollectionOperations =
            new List<Operation<string, (Line, Line)?>>();

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

            foreach (Operation<string, (Line, Line)?> operations in textCollectionOperations)
            {
                switch (operations.OperationType)
                {
                    case OperationType.ADDITION:
                        if (operations.Value.HasValue)
                            data.textCollectionDictionary.Add(operations.Key, operations.Value.Value);
                        break;
                    case OperationType.MODIFICATION:
                        if (operations.Value.HasValue)
                            data.textCollectionDictionary[operations.Key] = operations.Value.Value;
                        break;
                    case OperationType.REMOVAL:
                        data.textCollectionDictionary.Remove(operations.Key);
                        break;
                }
            }
        }

        public bool HasErrors()
        {
            return m_invalidOperations.Count != 0;
        }
        
        public string ConflictErrors()
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

        public string HumanErrors()
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
            result.AppendLine("Conflict summary");
            result.AppendLine("================");
            result.AppendLine("");
            result.AppendLine("A");
            result.AppendLine(">>>>>>>");
            result.AppendLine(a.ToString());
            result.AppendLine(">>>>>>>");
            result.AppendLine("");
            result.AppendLine("B");
            result.AppendLine("<<<<<<<");
            result.AppendLine(b.ToString());
            result.AppendLine("<<<<<<<");
            return result.ToString();
        }



    }

    public enum OperationType
    {
        ADDITION,
        MODIFICATION,
        REMOVAL,
    }

    public abstract class Operation
    {
    }

    public class Operation<T, U> : Operation
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

        protected bool Equals(Operation<T, U> other)
        {
            return OperationType == other.OperationType && EqualityComparer<T>.Default.Equals(Key, other.Key) &&
                   EqualityComparer<U>.Default.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Operation<T, U>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) OperationType;
                hashCode = (hashCode * 397) ^ EqualityComparer<T>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ EqualityComparer<U>.Default.GetHashCode(Value);
                return hashCode;
            }
        }
    }

    public class YAMLParser
    {
        public bool Parse(string pathSource, string pathA, string pathB, string pathMerged, string destinationPath,
            bool createBackup)
        {
            Document yamlSource;
            Data sourceData;
            ModificationResult modificationResult;

            string diagnosticFilePath = null;

            if (destinationPath != null)
            {
                string directoryName = Path.GetDirectoryName(destinationPath) ?? "";

                string guid = Guid.NewGuid().ToString();

                diagnosticFilePath = Path.Combine(directoryName, $"CONFLICTS_SUMMARY_{guid}");

                if (createBackup)
                {
                    string pathABackup = Path.Combine(directoryName, $"{guid}_LOCAL");
                    string pathBBackup = Path.Combine(directoryName, $"{guid}_REMOTE");
                    string pathSourceBackup = Path.Combine(directoryName, $"{guid}_BASE");
                    File.Copy(pathA, pathABackup, true);
                    File.Copy(pathB, pathBBackup, true);
                    File.Copy(pathSource, pathSourceBackup, true);
                }
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

                        string[] textNames =
                        {
                            "m_textKeyDictionary",
                            "m_dataDictionary",
                        };
                        string[] versionsNames =
                        {
                            "m_editorInfoDictionary",
                        };
                        string[] textCollectionNames =
                        {
                            "m_textCollectionDictionary",
                        };

                        yamlSource = new Document(textNames, versionsNames, textCollectionNames);
                        yamlSource.Parse(streamReader);
                        sourceData = ParseDocument(yamlSource);

                        Document yamlSourceA = new Document(textNames, versionsNames, textCollectionNames);
                        yamlSourceA.Parse(streamReaderA);
                        Data aData = ParseDocument(yamlSourceA);
                        Document yamlSourceB = new Document(textNames, versionsNames, textCollectionNames);
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

            File.Delete(pathMerged);

            
            using (FileStream fs2 = File.Open(pathMerged, FileMode.OpenOrCreate, FileAccess.Write,
                FileShare.None))
            {
                StreamWriter output = new StreamWriter(fs2, new UTF8Encoding(false)) {NewLine = "\n"};
                yamlSource.Save(output);
                output.Flush();
                        
                if (diagnosticFilePath == null && modificationResult.HasErrors())
                {
                    string errors = modificationResult.ConflictErrors();
                    if (!string.IsNullOrWhiteSpace(errors))
                    {
                        output.WriteLine(errors);
                    }
                    output.Flush();
                    return false;
                }
            }

            if (diagnosticFilePath != null && modificationResult.HasErrors())
            {
                string errors = modificationResult.HumanErrors();
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    using (FileStream fs2 = File.Open(diagnosticFilePath, FileMode.OpenOrCreate, FileAccess.Write,
                        FileShare.None))
                    {
                        StreamWriter output = new StreamWriter(fs2, new UTF8Encoding(false)) {NewLine = "\n"};
                        output.WriteLine(errors);
                        output.Flush();
                    }
                }
            }

            return true;
        }


        public ModificationResult ComputeResults(Modifications a, Modifications b)
        {
            ModificationResult result = new ModificationResult();

            // Add all the A operations, provided they're not in B, or equal.
            // Remove all B operations with identical key.
            foreach (KeyValuePair<int, Operation<int, Line>> pair in a.textKeyOperations)
            {
                var key = pair.Key;
                var opA = pair.Value;
                var valid = true;
                if (b.textKeyOperations.TryGetValue(key, out Operation<int, Line> opB))
                {
                    valid = opB.Equals(opA);
                }

                if (valid)
                {
                    result.textKeyOperations.Add(opA);
                }
                else
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(opA, opB));
                }

                b.textKeyOperations.Remove(key);
            }

            foreach (KeyValuePair<int, Operation<int, Line>> pair in a.versionOperations)
            {
                var key = pair.Key;
                var opA = pair.Value;
                var valid = true;
                if (b.versionOperations.TryGetValue(key, out Operation<int, Line> opB))
                {
                    valid = opB.Equals(opA);
                }

                if (valid)
                {
                    result.versionOperations.Add(opA);
                }
                else
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(opA, opB));
                }

                b.versionOperations.Remove(key);
            }

            foreach (KeyValuePair<string, Operation<string, (Line, Line)?>> pair in a.textCollectionOperations)
            {
                var key = pair.Key;
                var opA = pair.Value;
                var valid = true;
                if (b.textCollectionOperations.TryGetValue(key, out Operation<string, (Line, Line)?> opB))
                {
                    valid = opB.Equals(opA);
                }

                if (valid)
                {
                    result.textCollectionOperations.Add(opA);
                }
                else
                {
                    result.m_invalidOperations.Add(new Tuple<Operation, Operation>(opA, opB));
                }

                b.textCollectionOperations.Remove(key);
            }

            // Add the remaining B operations
            foreach (KeyValuePair<int, Operation<int, Line>> operation in b.textKeyOperations)
            {
                result.textKeyOperations.Add(operation.Value);
            }

            foreach (KeyValuePair<int, Operation<int, Line>> operation in b.versionOperations)
            {
                result.versionOperations.Add(operation.Value);
            }

            foreach (KeyValuePair<string, Operation<string, (Line, Line)?>> operation in b.textCollectionOperations)
            {
                result.textCollectionOperations.Add(operation.Value);
            }

            return result;
        }

        public Modifications GenerateDiffs(Data source, Data modif)
        {
            Modifications modifications = new Modifications();
            foreach (KeyValuePair<int, Line> pair in source.textKeyDictionary)
            {
                if (modif.textKeyDictionary.TryGetValue(pair.Key, out Line value))
                {
                    if (!string.Equals(value.ScalarValue, pair.Value.ScalarValue))
                    {
                        modifications.textKeyOperations.Add(pair.Key,
                            new Operation<int, Line>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.textKeyOperations.Add(pair.Key,
                        new Operation<int, Line>(pair.Key, null, OperationType.REMOVAL));
                }
            }

            foreach (KeyValuePair<int, Line> pair in modif.textKeyDictionary)
            {
                if (!source.textKeyDictionary.Contains(pair.Key))
                {
                    modifications.textKeyOperations.Add(pair.Key,
                        new Operation<int, Line>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }

            foreach (KeyValuePair<int, Line> pair in source.versionDictionary)
            {
                if (modif.versionDictionary.TryGetValue(pair.Key, out Line value))
                {
                    if (!string.Equals(value.ScalarValue, pair.Value.ScalarValue))
                    {
                        modifications.versionOperations.Add(pair.Key,
                            new Operation<int, Line>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.versionOperations.Add(pair.Key,
                        new Operation<int, Line>(pair.Key, null, OperationType.REMOVAL));
                }
            }

            foreach (KeyValuePair<int, Line> pair in modif.versionDictionary)
            {
                if (!source.versionDictionary.Contains(pair.Key))
                {
                    modifications.versionOperations.Add(pair.Key,
                        new Operation<int, Line>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }

            foreach (KeyValuePair<string, (Line, Line)> pair in source.textCollectionDictionary)
            {
                if (modif.textCollectionDictionary.TryGetValue(pair.Key, out (Line, Line) value))
                {
                    if (!string.Equals(value.Item2.ScalarValue, pair.Value.Item2.ScalarValue))
                    {
                        modifications.textCollectionOperations.Add(pair.Key,
                            new Operation<string, (Line, Line)?>(pair.Key, value, OperationType.MODIFICATION));
                    }
                }
                else
                {
                    modifications.textCollectionOperations.Add(pair.Key,
                        new Operation<string, (Line, Line)?>(pair.Key, null, OperationType.REMOVAL));
                }
            }

            foreach (KeyValuePair<string, (Line, Line)> pair in modif.textCollectionDictionary)
            {
                if (!source.textCollectionDictionary.Contains(pair.Key))
                {
                    modifications.textCollectionOperations.Add(pair.Key,
                        new Operation<string, (Line, Line)?>(pair.Key, pair.Value, OperationType.ADDITION));
                }
            }

            return modifications;
        }


        private void WriteDocument(Document document, Data data)
        {
            if (document.HasTextDictionary) WriteTextKeyDictionary(document, data);
            if (document.HasVersionDictionary) WriteVersionDictionary(document, data);
            if (document.HasTextCollectionDictionary) WriteTextCollectionDictionary(document, data);
        }

        private Data ParseDocument(Document document)
        {
            Data data = new Data();
            if (document.HasTextDictionary) ParseTextKeyDictionary(document, data);
            if (document.HasVersionDictionary) ParseVersionDictionary(document, data);
            if (document.HasTextCollectionDictionary) ParseTextCollectionDictionary(document, data);

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

        private void ParseTextCollectionDictionary(Document document, Data data)
        {
            var keys = document.TextCollectionDictionaryKeys;
            var values = document.TextCollectionDictionaryValues;
            if (keys.Count != values.Count)
            {
                throw new InvalidOperationException("The number of keys doesn't match the number of values");
            }

            for (int i = 0, size = keys.Count; i < size; ++i)
            {
                data.textCollectionDictionary.Add(keys[i].ScalarValue, (keys[i], values[i]));
            }
        }

        private void WriteTextCollectionDictionary(Document document, Data data)
        {
            List<Line> keys = new List<Line>();
            List<Line> values = new List<Line>();

            foreach (KeyValuePair<string, (Line, Line)> pair in data.textCollectionDictionary)
            {
                var (key, value) = pair.Value;
                keys.Add(key);
                values.Add(value);
            }

            document.TextCollectionDictionaryKeys = keys;
            document.TextCollectionDictionaryValues = values;
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
                string substring = ReverseHexString(serializedIntArray.Substring(i * 8, 8));
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