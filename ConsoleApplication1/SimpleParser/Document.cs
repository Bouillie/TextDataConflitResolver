using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleApplication1.SimpleParser
{
    internal enum State
    {
        ParsingRaw,
        ParsingTextKeys,
        ParsingTextValues,
        ParsingVersionKeys,
        ParsingVersionValues,
    }
    
    public class Document
    {
        private string[] m_textKeyDictionaryName;
        private string[] m_versionDictionaryName;

        private Block m_blockChain;
        
        private string m_textDictionaryKeys;
        private Line m_textDictionaryKeysLine;
        private Block m_textDictionaryValuesBlock;
        
        private string m_versionDictionaryKeys;
        private Line m_versionDictionaryKeysLine;
        private string m_versionDictionaryValues;
        private Line m_versionDictionaryValuesLine;

        public Document(string[] mTextKeyDictionaryName, string[] mVersionDictionaryName)
        {
            m_textKeyDictionaryName = mTextKeyDictionaryName;
            m_versionDictionaryName = mVersionDictionaryName;
        }

        public string TextDictionaryKeys
        {
            get { return m_textDictionaryKeys;}
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
            get { return m_textDictionaryValuesBlock.Lines;}
            set
            {
                if (m_textDictionaryValuesBlock == null) return;   
                m_textDictionaryValuesBlock.Lines = value;
            }
        }
        
        public string VersionDictionaryKeys
        {
            get { return m_versionDictionaryKeys;}
            set { 
                m_versionDictionaryKeys = value; 
                string s = m_versionDictionaryKeysLine.Value;
                string replacement = m_keysRegex.Replace(s, "$1") + value;
                m_versionDictionaryKeysLine.SetValue(replacement);
            }
        }
        
        public string VersionDictionaryValues
        {
            get { return m_versionDictionaryValues;}
            set { 
                m_versionDictionaryValues = value; 
                string s = m_versionDictionaryValuesLine.Value;
                string replacement = m_keysRegex.Replace(s, "$1") + value;
                m_versionDictionaryValuesLine.SetValue(replacement);
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

                    currentBlock.Lines.Add(new Line(line));
                    line = reader.ReadLine();
                } 
                else if (state == State.ParsingTextKeys)
                {
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
                else if (state == State.ParsingTextValues)
                {
                    if (depth <= dictionaryDepth)
                    {
                        state = State.ParsingRaw;
                        currentBlock = NewBlock(currentBlock);
                    }
                    else
                    {
                        if (line.TrimStart(' ').StartsWith("-"))
                        {
                            Line l = new Line(line);
                            l.SetScalarDepth(depth+2);
                            currentBlock.Lines.Add(l);
                        }
                        else
                        {
                            List<Line> lines = currentBlock.Lines;
                            Line l = lines[lines.Count - 1];
                            l.AppendValue(line);
                        }
                        line = reader.ReadLine();
                    }
                }
                else if (state == State.ParsingVersionKeys)
                {
                    Match match = m_keysRegex.Match(line);
                    GroupCollection groups = match.Groups;
                    m_versionDictionaryKeys = groups[2].Value;
                    m_versionDictionaryKeysLine = new Line(line);
                    currentBlock.Lines.Add(m_versionDictionaryKeysLine);
                    state = State.ParsingVersionValues;
                    line = reader.ReadLine();
                }
                else if (state == State.ParsingVersionValues)
                {
                    if (depth <= dictionaryDepth)
                    {
                        state = State.ParsingRaw;
                        currentBlock = NewBlock(currentBlock);
                    }
                    else
                    {
                        Match match = m_keysRegex.Match(line);
                        GroupCollection groups = match.Groups;
                        m_versionDictionaryValues = groups[2].Value;
                        m_versionDictionaryValuesLine = new Line(line);
                        currentBlock.Lines.Add(m_versionDictionaryValuesLine);
                        state = State.ParsingRaw;
                        currentBlock = NewBlock(currentBlock);
                        line = reader.ReadLine();
                    }
                }


            }
        }
    }
}