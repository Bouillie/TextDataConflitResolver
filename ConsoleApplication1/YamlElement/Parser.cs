using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ConsoleApplication1.YamlElement
{
    public class Buffer
    {
        private const int BufferSize = 8;
        private char[] m_buffer = new char[BufferSize];
        private StreamReader m_reader;
        private int m_index;

        public Buffer(StreamReader mReader)
        {
            m_reader = mReader;
            FillBuffer(BufferSize);
        }

        public bool isEOS(int offset)
        {
            return m_buffer[OffsetToIndex(offset)] == char.MinValue;
        }

        private void FillBuffer(int count)
        {
            if (count > BufferSize)
                return;

            bool eos = false;
            for (int i = 0, size = count; i < size; ++i)
            {
                int index = OffsetToIndex(i);
                if (eos)
                {
                    m_buffer[index] = char.MinValue;
                }
                else
                {
                    int read = m_reader.Read(m_buffer, index, 1);
                    if (read != 1)
                    {
                        eos = true;
                        m_buffer[index] = char.MinValue;
                    }
                }
            }
        }
        
        private int OffsetToIndex(int offset)
        {
            return (m_index + offset) % BufferSize;
        }
        
        private void AdvanceIndex()
        {
            m_reader.Read(m_buffer, m_index, 1);
            m_index = (m_index + 1) % BufferSize;
        }
        
        public char Read()
        {
            char c = m_buffer[m_index];
            AdvanceIndex();
            return c;
        }

        public char Peek(int offset)
        {
            return m_buffer[OffsetToIndex(offset)];
        }
        
        public bool Check(char c, int offset)
        {
            return m_buffer[OffsetToIndex(offset)] == c;
        }
    }
    
    public class Parser
    {
        private int m_depth = -1;
        private Buffer m_buffer;
        
        public void Parse(StreamReader reader)
        {
            m_buffer = new Buffer(reader);
            YamlNode currentNode = new RootNode();
            
            while (!m_buffer.isEOS(0))
            {
                int depth = 0;
                while (m_buffer.Check(' ', 0))
                {
                    depth++;
                    m_buffer.Read();
                }

                char c = m_buffer.Peek(0);
                if (c == '-' && m_buffer.Check(' ', 1))
                {
                    // Sequence
                } 
                else if (c == '{' || char.IsLetter(c))
                {
                    // Mapping
                }
                else
                {
                    // Verbatim
                }

            }
        }

        private NodeType GetNextNodeType(Buffer buffer)
        {
            
        }
        
        private MappingNode ParseMapping()
        {
            MappingNode mappingNode = new MappingNode();
        }
        
        private void ParseSequence()
        {
            
        }
        
        private void ParseScalar()
        {
            
        }
    }

    public enum NodeType
    {
        Scalar,
        Mapping,
        Sequence,
        Verbatim
    }
}