using System;
using System.Collections.Generic;
using System.IO;

namespace ConsoleApplication1.YamlElement
{
    public abstract class YamlNode
    {
        protected YamlNode m_parent;
        protected int m_depth;

        public abstract bool isOneLiner { get; }
        
        protected string Padding()
        {
            return new string(' ', m_depth * 2);
        }
        
        public abstract void Serialize(StreamWriter stream, bool fromNewLine, bool asOneLiner);
    }
}