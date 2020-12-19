using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinApiMapper
{
    class TypeMember
    {
        public TypeMember(string offset, string type, string name)
        {
            Offset = int.Parse(offset, System.Globalization.NumberStyles.HexNumber);
            Type = type;
            Name = name;
        }

        public int Offset;
        public string Type;
        public string Name;
    }
}
