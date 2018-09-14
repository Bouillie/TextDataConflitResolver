using System.Collections.Generic;
using System.IO;

namespace ConsoleApplication1.YamlElement
{
    public class SequenceNode: YamlNode
    {
        private readonly List<YamlNode> m_children = new List<YamlNode>();
        private readonly bool m_multiline;

        public List<YamlNode> Children => m_children;

        public override bool isOneLiner => false;

        public override void Serialize(StreamWriter stream, bool fromNewLine, bool asOneLiner)
        {
            foreach (YamlNode node in m_children)
            {
                stream.Write(Padding());
                stream.Write("- ");
                node.Serialize(stream, false, node.isOneLiner);
            }
        }
    }
}