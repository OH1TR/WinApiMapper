using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WinApiMapper
{
    /*
    Dia2Dump.exe: PrintSymbol.cpp is patched around line 1807:
      
    case SymTagTypedef:
        BSTR bstrName;
        if (pSymbol->get_name(&bstrName) == S_OK) {
            wprintf(L"%s", bstrName);
            SysFreeString(bstrName);
        }
        PrintSymbolType(pSymbol);
        break;
    case SymTagVTable:
      PrintSymbolType(pSymbol);
      break;
     */


    class Program
    {
        static Dictionary<string, string> Typedef = new Dictionary<string, string>();
        static Dictionary<string, List<TypeMember>> Types = new Dictionary<string, List<TypeMember>>();
        static void Main(string[] args)
        {
            try
            {
                string target = args[1];
                string dumpFile = BuildTempProject(target, int.Parse(args[0]));
                ParseDump(dumpFile);
                string realType = FindType(target,out int ptrLevel);
                if (realType != null)
                {
                    PrintType(realType, ptrLevel);
                }
                else
                {
                    Console.WriteLine("Failed, can you find it here: " + dumpFile);
                    Console.WriteLine("Or try these:");

                    foreach (var t in Types.Where(i => i.Key.Contains(target)))
                        Console.WriteLine(t.Value);

                    foreach (var t in Typedef.Where(i => i.Key.Contains(target)))
                        Console.WriteLine(t.Key + " : " + t.Value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + ex.StackTrace);
                Console.WriteLine("Usage: WinApiMapper.exe: 32 _CONTEXT");
            }
        }

        static string FindType(string target,out int ptrLevel)
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

        static void PrintType(string target, int ptrLevel)
        {
            Console.WriteLine(target +" "+ new String('*', ptrLevel) + " :");
            var t = Types[target];
            foreach (var m in t)
            {
                Console.WriteLine("offset: 0x" + m.Offset.ToString("x") + " " + m.Type + " " + m.Name);
            }
        }

        static void ParseDump(string file)
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
                    members.Add(new TypeMember(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
                    continue;
                }

                m = Regex.Match(line, @"UserDefinedType:(\s*)([\w\d_]+)");
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

                    typeName = m.Groups[2].Value;
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


        static string BuildTempProject(string targetType, int bits)
        {
            string platform = bits == 32 ? "x86" : "x64";
            string debugFolder = bits == 32 ? "Debug" : "x64\\Debug";
            string temp = Path.GetTempPath();
            string slnDir = Path.Combine(temp, Guid.NewGuid().ToString());
            string cpp = Path.Combine(slnDir, "TempProject.cpp");

            Directory.CreateDirectory(slnDir);
            CopyFile("Resources\\TempProject.cpp", slnDir);
            CopyFile("Resources\\TempProject.sln", slnDir);
            CopyFile("Resources\\TempProject.vcxproj", slnDir);
            CopyFile("Resources\\Dia2Dump.exe", slnDir);

            var txt = File.ReadAllText(cpp);
            txt = txt.Replace("//REPLACEME", targetType + " var;");
            File.WriteAllText(cpp, txt);

            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            sb.Append("msbuild " + slnDir + "\\TempProject.sln /p:Configuration=Debug /p:Platform=\"" + platform + "\"");
            sb.Append(Environment.NewLine);
            sb.Append(@"if NOT [""%errorlevel%""]==[""0""] pause");
            sb.Append(Environment.NewLine);
            sb.Append("\"" + slnDir + "\\Dia2Dump.exe\" -all \"" + slnDir + "\\" + debugFolder + "\\TempProject.pdb\" > \"" + slnDir + "\\dump.txt\"");
            sb.Append(Environment.NewLine);
            sb.Append("cd \"" + slnDir + "\"");
            sb.Append(Environment.NewLine);
            File.WriteAllText(Path.Combine(slnDir, "build.bat"), sb.ToString());
            RunCommand(Path.Combine(slnDir, "build.bat"));
            return Path.Combine(slnDir, "dump.txt");
        }



        static void CopyFile(string name, string dstFolder)
        {
            File.Copy(name, Path.Combine(dstFolder, Path.GetFileName(name)));
        }
        static void RunCommand(string command)
        {
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = "/C \"" + command + "\"";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            process.Start();
            process.WaitForExit();
        }
    }
}
