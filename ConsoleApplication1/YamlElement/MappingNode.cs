using System.Collections.Generic;
using System.IO;
using ConsoleApplication1.Utils;

namespace ConsoleApplication1.YamlElement
{
    public class MappingNode: YamlNode
    {
        private readonly SortedListDictionary<string, YamlNode> m_children = new SortedListDictionary<string, YamlNode>();
        public SortedListDictionary<string, YamlNode> Children => m_children;

        public override bool isOneLiner
        {
            get
            {
                foreach (KeyValuePair<string,YamlNode> pair in m_children)
                {
                    if (!pair.Value.isOneLiner)
                        return false;
                }
                return true;
            }
        }

        public override void Serialize(StreamWriter stream, bool fromNewLine, bool asOneLiner)
        {
            string padding = Padding();
            if (fromNewLine)
                stream.Write(padding);
            
            if (asOneLiner)
            {
                bool hasManyChildren = m_children.Count > 1;
                if (hasManyChildren)
                    stream.Write("{");
                bool first = true;
                foreach (KeyValuePair<string, YamlNode> pair in m_children)
                {
                    if (first) first = false;
                    else stream.Write(", ");
                    stream.Write($"{pair.Key}: ");
                    pair.Value.Serialize(stream, false, true);
                }
                if (hasManyChildren)
                    stream.Write("}");
                stream.Write(stream.NewLine);
            }
            else
            {
                bool first = true;
                foreach (KeyValuePair<string, YamlNode> pair in m_children)
                {
                    if (first) first = false;
                    else stream.Write(stream.NewLine);

                    stream.Write(padding);
                    stream.Write($"{pair.Key}:");
                    YamlNode node = pair.Value;
                    if (node is ScalarNode)
                    {
                        stream.Write(" ");
                        node.Serialize(stream, false, false);
                    }
                    else
                    {
                        stream.Write(stream.NewLine);
                        node.Serialize(stream, true, false);
                    }
                    stream.Write(stream.NewLine);
                }
            }
        }
    }
}