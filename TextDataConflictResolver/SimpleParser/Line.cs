using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TextDataConflictResolver.SimpleParser
{
    public class Line
    {
        public string Value { get; private set; }
        private readonly List<string> m_rawStrings = new List<string>();

        public Line(string value)
        {
            Value = value;
            m_rawStrings.Add(value);
        }

        public void SetValue(string value)
        {
            Value = value;
            m_rawStrings.Clear();
            m_rawStrings.Add(value);
        }
        
        public void AppendValue(string value)
        {
            Value += value;
            m_rawStrings.Add(value);
        }

        private int m_depth;
        public void SetScalarDepth(int depth)
        {
            m_depth = depth;
        }

        public string ScalarValue
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                for (int index = 0; index < m_rawStrings.Count; index++)
                {
                    if (index != 0)
                        sb.Append(" ");
                    string s = m_rawStrings[index];
                    int startIndex = Math.Max(0, Math.Min(s.Length - 1, m_depth));
                    sb.Append(s.Substring(startIndex, s.Length - startIndex));
                }
                return sb.ToString();
            }
        }

        public void Serialize(TextWriter writer)
        {
            foreach (string s in m_rawStrings)
            {
                writer.WriteLine(s);
            }
        }


        public override string ToString()
        {
            return ScalarValue;
        }
    }
}