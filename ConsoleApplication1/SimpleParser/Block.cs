using System.Collections.Generic;
using System.IO;

namespace ConsoleApplication1.SimpleParser
{
    public class Block
    {
        public Block Previous;
        public Block Next;

        public List<Line> Lines = new List<Line>();

        public void Serialize(TextWriter writer)
        {
            foreach (Line line in Lines)
            {
                line.Serialize(writer);
            }
            
            Next?.Serialize(writer);
        }
    }
}