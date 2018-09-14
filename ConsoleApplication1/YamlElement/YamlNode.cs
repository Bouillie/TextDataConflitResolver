using System.Collections.Generic;

namespace ConsoleApplication1.YamlElement
{
    public abstract class YamlNode
    {
        protected YamlNode m_parent;
        protected List<YamlNode> m_children = new List<YamlNode>();
        protected int m_depth;
        
        
    }
}