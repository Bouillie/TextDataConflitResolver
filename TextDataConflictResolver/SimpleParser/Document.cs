using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TextDataConflictResolver.SimpleParser
{
    internal enum State
    {
        ParsingRaw,
        ParsingTextKeys,
        ParsingTextValues,
        ParsingVersionKeys,
        ParsingVersionValuesVersion,
        ParsingVersionValuesComment,
        ParsingTextCollectionKeys,
        ParsingTextCollectionValues,
    }

    public class Document
    {
        private string[] m_textKeyDictionaryName;
        private string[] m_textCollectionDictionaryName;
        private string[] m_versionDictionaryName;

        private Block m_blockChain;

        private string m_textDictionaryKeys;
        private Line m_textDictionaryKeysLine;
        private Block m_textDictionaryValuesBlock;

        private string m_versionDictionaryKeys;
        private Line m_versionDictionaryKeysLine;
        private Block m_versionDictionaryValuesBlock;

        private Block m_textCollectionDictionaryKeysBlock;
        private Block m_textCollectionDictionaryValuesBlock;

        private bool m_hasTextDictionary;
        private bool m_hasVersionDictionary;
        private bool m_hasTextCollectionDictionary;

        public bool HasTextDictionary => m_hasTextDictionary;
        public bool HasVersionDictionary => m_hasVersionDictionary;
        public bool HasTextCollectionDictionary => m_hasTextCollectionDictionary;

        public Document(string[] textKeyDictionaryName, string[] versionDictionaryName, string[] textCollectionNames)
        {
            m_textKeyDictionaryName = textKeyDictionaryName;
            m_versionDictionaryName = versionDictionaryName;
            m_textCollectionDictionaryName = textCollectionNames;
        }

        public string TextDictionaryKeys
        {
            get { return m_textDictionaryKeys; }
            set
            {
                m_textDictionaryKeys = value;
                string s = m_textDictionaryKeysLine.Value;
                string replacement = m_keysRegex.Replace(s, "$1") + value;
                m_textDictionaryKeysLine.SetValue(replacement);
            }
        }

        public List<Line> TextDictionaryValues
        {
            get { return m_textDictionaryValuesBlock.Lines; }
            set
            {
                if (m_textDictionaryValuesBlock == null) return;
                m_textDictionaryValuesBlock.Lines = value;
            }
        }

        public string VersionDictionaryKeys
        {
            get { return m_versionDictionaryKeys; }
            set
            {
                m_versionDictionaryKeys = value;
                string s = m_versionDictionaryKeysLine.Value;
                string replacement = m_keysRegex.Replace(s, "$1") + value;
                m_versionDictionaryKeysLine.SetValue(replacement);
            }
        }

        public List<Line> VersionDictionaryValues
        {
            get { return m_versionDictionaryValuesBlock.Lines; }
            set
            {
                if (m_versionDictionaryValuesBlock == null) return;
                m_versionDictionaryValuesBlock.Lines = value;
            }
        }

        public List<Line> TextCollectionDictionaryKeys
        {
            get { return m_textCollectionDictionaryKeysBlock.Lines; }
            set
            {
                if (m_textCollectionDictionaryKeysBlock == null) return;
                m_textCollectionDictionaryKeysBlock.Lines = value;
            }
        }

        public List<Line> TextCollectionDictionaryValues
        {
            get { return m_textCollectionDictionaryValuesBlock.Lines; }
            set
            {
                if (m_textCollectionDictionaryValuesBlock == null) return;
                m_textCollectionDictionaryValuesBlock.Lines = value;
            }
        }

        public void Save(TextWriter writer)
        {
            m_blockChain.Serialize(writer);
        }

        private static int CountStartSpaces(string line)
        {
            int depth = 0;
            for (int i = 0; i < line.Length; ++i)
            {
                if (line[i] == ' ')
                    depth++;
                else
                    break;
            }

            return depth;
        }

        private Block NewBlock(Block currentBlock)
        {
            Block newBlock = new Block();
            currentBlock.Next = newBlock;
            newBlock.Previous = currentBlock;
            return newBlock;
        }

        private Regex m_keysRegex = new Regex("([ _0-9a-zA-Z]+: )([0-9a-fA-F]+)");

        private bool ContainsLine(string line, string[] names)
        {
            foreach (string s in names)
            {
                if (line.Contains($"{s}:"))
                    return true;
            }

            return false;
        }

        public void Parse(TextReader reader)
        {
            Block currentBlock = m_blockChain = new Block();
            State state = State.ParsingRaw;
            string line = reader.ReadLine();
            char currentEscapeChar = (char) 0;
            bool isInEscape = false;
            int dictionaryDepth = 0;
            while (line != null)
            {
                int depth = CountStartSpaces(line);

                if (state == State.ParsingRaw)
                {
                    if (ContainsLine(line, m_textKeyDictionaryName))
                    {
                        state = State.ParsingTextKeys;
                        dictionaryDepth = depth;
                    }
                    else if (ContainsLine(line, m_versionDictionaryName))
                    {
                        state = State.ParsingVersionKeys;
                        dictionaryDepth = depth;
                        currentBlock = NewBlock(currentBlock);
                    }
                    else if (ContainsLine(line, m_textCollectionDictionaryName))
                    {
                        state = State.ParsingTextCollectionKeys;
                        dictionaryDepth = depth;
                        currentBlock = NewBlock(currentBlock);
                    }

                    currentBlock.Lines.Add(new Line(line));
                    line = reader.ReadLine();
                }
                else if (state == State.ParsingTextKeys)
                {
                    m_hasTextDictionary = true;
                    Match match = m_keysRegex.Match(line);
                    GroupCollection groups = match.Groups;
                    m_textDictionaryKeys = groups[2].Value;
                    state = State.ParsingTextValues;
                    m_textDictionaryKeysLine = new Line(line);
                    currentBlock.Lines.Add(m_textDictionaryKeysLine);
                    line = reader.ReadLine(); // m_values:
                    currentBlock.Lines.Add(new Line(line));
                    line = reader.ReadLine();
                    currentBlock = m_textDictionaryValuesBlock = NewBlock(currentBlock);
                }
                else if (state == State.ParsingVersionKeys)
                {
                    m_hasVersionDictionary = true;
                    Match match = m_keysRegex.Match(line);
                    GroupCollection groups = match.Groups;
                    m_versionDictionaryKeys = groups[2].Value;
                    state = State.ParsingVersionValuesVersion;
                    m_versionDictionaryKeysLine = new Line(line);
                    currentBlock.Lines.Add(m_versionDictionaryKeysLine);
                    line = reader.ReadLine(); // m_values:
                    currentBlock.Lines.Add(new Line(line));
                    line = reader.ReadLine();
                    currentBlock = m_versionDictionaryValuesBlock = NewBlock(currentBlock);
                }
                else if (state == State.ParsingTextCollectionKeys)
                {
                    if (!m_hasTextCollectionDictionary)
                    {
                        m_hasTextCollectionDictionary = true;
                        currentBlock.Lines.Add(new Line(line)); // m_keys: 
                        line = reader.ReadLine();
                        currentBlock = m_textCollectionDictionaryKeysBlock = NewBlock(currentBlock);
                    }
                    else if (line.TrimStart(' ').StartsWith("m_values:") && !isInEscape)
                    {
                        state = State.ParsingTextCollectionValues;
                        currentBlock = NewBlock(currentBlock);
                        currentBlock.Lines.Add(new Line(line)); // m_values: 
                        line = reader.ReadLine();
                        currentBlock = m_textCollectionDictionaryValuesBlock = NewBlock(currentBlock);
                    }
                    else
                    {
                        if (line.TrimStart(' ').StartsWith("-") && !isInEscape)
                        {
                            int dataDepth = depth + 2;
                            currentEscapeChar = line.Length > dataDepth ? line[dataDepth] : (char) 0;
                            isInEscape = currentEscapeChar == '\'' || currentEscapeChar == '"';
                            Line l = new Line(line);
                            l.SetScalarDepth(dataDepth);
                            currentBlock.Lines.Add(l);
                        }
                        else
                        {
                            List<Line> lines = currentBlock.Lines;
                            Line l = lines[lines.Count - 1];
                            l.AppendValue(line);
                        }

                        if (isInEscape && line.EndsWith(currentEscapeChar.ToString()))
                        {
                            currentEscapeChar = (char) 0;
                            isInEscape = false;
                        }

                        line = reader.ReadLine();
                    }
                }
                else if (state == State.ParsingTextValues || state == State.ParsingTextCollectionValues)
                {
                    if (depth <= dictionaryDepth && !isInEscape)
                    {
                        state = State.ParsingRaw;
                        currentBlock = NewBlock(currentBlock);
                    }
                    else
                    {
                        if (line.TrimStart(' ').StartsWith("-") && !isInEscape)
                        {
                            int dataDepth = depth + 2;
                            currentEscapeChar = line.Length > dataDepth ? line[dataDepth] : (char) 0;
                            isInEscape = currentEscapeChar == '\'' || currentEscapeChar == '"';
                            Line l = new Line(line);
                            l.SetScalarDepth(dataDepth);
                            currentBlock.Lines.Add(l);
                        }
                        else
                        {
                            List<Line> lines = currentBlock.Lines;
                            Line l = lines[lines.Count - 1];
                            l.AppendValue(line);
                        }

                        if (isInEscape && line.EndsWith(currentEscapeChar.ToString()))
                        {
                            currentEscapeChar = (char) 0;
                            isInEscape = false;
                        }

                        line = reader.ReadLine();
                    }
                }
                else if (state == State.ParsingVersionValuesVersion)
                {
                    if (depth <= dictionaryDepth && !isInEscape)
                    {
                        state = State.ParsingRaw;
                        currentBlock = NewBlock(currentBlock);
                    }
                    else
                    {
                        state = State.ParsingVersionValuesComment;
                        Line l = new Line(line);
                        l.SetScalarDepth(depth + 2);
                        currentBlock.Lines.Add(l);
                        line = reader.ReadLine();
                    }
                }
                else if (state == State.ParsingVersionValuesComment)
                {
                    List<Line> lines = currentBlock.Lines;
                    Line l = lines[lines.Count - 1];
                    l.AppendValue(line);

                    if (!isInEscape) /* if (line.TrimStart(' ').StartsWith("comment: ")) */
                        // Always true is the file is correctly formatted
                    {
                        const int delta = 9; // = "comment: ".Length
                        int escapeCharIndex = depth + delta;
                        currentEscapeChar = line.Length > escapeCharIndex ? line[escapeCharIndex] : (char) 0;
                        isInEscape = currentEscapeChar == '\'' || currentEscapeChar == '"';
                    }

                    if (isInEscape && line.EndsWith(currentEscapeChar.ToString())) // Can happen on the first line
                    {
                        currentEscapeChar = (char) 0;
                        isInEscape = false;
                    }

                    if (!isInEscape)
                    {
                        state = State.ParsingVersionValuesVersion;
                    }

                    line = reader.ReadLine();
                }
            }
        }
    }
}