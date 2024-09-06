using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinApiMapper
{
    class StructParser
    {
        public Dictionary<string, string> Typedef = new Dictionary<string, string>();
        public Dictionary<string, List<TypeMember>> Types = new Dictionary<string, List<TypeMember>>();


        public void ProcessStruct(string name,int bits)
        {
            Builder builder = new Builder();
            string target = name;
            string slnPath = builder.BuildTempProject(target, bits, false);
            string dumpFile = Path.Combine(slnPath, "dump.txt");
            ParseDump(dumpFile);
        }

        string MangleName(string name)
        {
            return name.Replace(":", "_").Replace("<unnamed-type-", "").Replace(">", "");
        }

        public void ParseDump(string file)
        {
            var lines = File.ReadAllLines(file);

            string mode = "";
            string typeName = "";
            List<TypeMember> members = new List<TypeMember>();
            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                    continue;

                Match m;
                m = Regex.Match(line, @"Data\s*:\s*this\+0x([0-9a-fA-FxX]+), Member, Type:\s*([^,]+),\s([\w\d_]+)");
                if (m.Success)
                {
                    members.Add(new TypeMember(m.Groups[1].Value, MangleName(m.Groups[2].Value),m.Groups[3].Value));
                    continue;
                }

                m = Regex.Match(line, @"UserDefinedType:(\s*)([\w\d_:<>-]+)");
                if (m.Success)
                {
                    if (mode == "UDT" && m.Groups[1].Value.Length > 1)
                        continue;

                    if (mode == "UDT")
                    {
                        if (!Types.ContainsKey(typeName))
                            Types.Add(typeName, members);

                        typeName = "";
                        members = new List<TypeMember>();
                        mode = "";
                    }

                    typeName = MangleName(m.Groups[2].Value);
                    mode = "UDT";
                    continue;
                }

                if (mode == "UDT")
                {
                    if (!Types.ContainsKey(typeName))
                        Types.Add(typeName, members);

                    typeName = "";
                    members = new List<TypeMember>();
                    mode = "";
                }

                m = Regex.Match(line, @"Typedef\s*:\s([\w\d_]*),\sType:\s*(.*)");
                if (m.Success)
                {
                    if (!Typedef.ContainsKey(m.Groups[1].Value))
                        Typedef.Add(m.Groups[1].Value, m.Groups[2].Value);
                    mode = "";
                }
            }
        }

        public string FindType(string target, out int ptrLevel)
        {
            ptrLevel = 0;
            if (Types.ContainsKey(target))
                return target;

            bool hit;
            do
            {
                hit = false;

                foreach (var t in Typedef)
                {
                    if (t.Key.Contains(target))
                    {
                        var tok = t.Value.Split(' ');
                        target = tok.Reverse().Where(i => i != "*").FirstOrDefault();
                        ptrLevel += tok.Where(i => i == "*").Count();
                        hit = true;
                    }
                }

                if (Types.ContainsKey(target))
                {
                    return target;
                }
            }
            while (hit);

            return null;
        }
    }
}
