using System.Collections.Generic;
using System.IO;

namespace ConsoleApplication1.YamlElement
{
    public class ScalarNode : YamlNode
    {
        public ScalarNode(string value)
        {
            Value = value;
            m_rawStrings.Add(value);
        }

        public string Value { get; private set; }
        private List<string> m_rawStrings = new List<string>();

        public void AppendLine(string value)
        {
            Value += value;
            m_rawStrings.Add(value);
        }

        public override bool isOneLiner => m_rawStrings.Count < 2;

        public override void Serialize(StreamWriter stream, bool fromNewLine, bool asOneLiner)
        {
            string padding = Padding();
            if (fromNewLine)
                stream.Write(padding);

            for (int i = 0, size = m_rawStrings.Count; i < size; ++i)
            {
                if (i != 0)
                {
                    stream.Write(stream.NewLine);
                    stream.Write(padding);
                }
                stream.Write(m_rawStrings[i]);
            }
        }
    }
}