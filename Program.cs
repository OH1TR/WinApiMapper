﻿using CommandLine;
using CppAst;
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
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                   .WithParsed<CommandLineOptions>(o =>
                   {
                       if (o.Struct != null)
                           DumpStruct(o);

                       if (o.Function != null)
                           DumpFunction(o);

                   });
        }

        static void DumpStruct(CommandLineOptions o)
        {
            try
            {
                string target = o.Struct;
                string slnPath = BuildTempProject(target, o.Bits, false);
                string dumpFile = Path.Combine(slnPath, "dump.txt");
                ParseDump(dumpFile);
                string realType = FindType(target, out int ptrLevel);
                if (realType != null)
                {
                    if (o.Mode == "frida")
                        PrintTypeFrida(realType, ptrLevel);
                    else
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
            }
        }

        static void DumpFunction(CommandLineOptions o)
        {
            var compilation = GetCompilation(o.Bits);

            var d = compilation.Functions.Where(i => string.Equals( i.Name,o.Function,StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            if(d!=null)
            {
                Console.WriteLine(d.ToString());
            }
            else
            {
                foreach(var f in compilation.Functions.Where(i => i.Name.StartsWith( o.Function, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine(f.ToString());
                }
            }
            return;
        }

        static CppCompilation GetCompilation(int bits)
        {
            string slnPath = BuildTempProject("int", bits, true);
            string targetFile = Path.Combine(slnPath, "TempProject.i");

            CppCompilation compilation;
            do
            {
                compilation = CppParser.ParseFile(targetFile, new CppParserOptions() { AutoSquashTypedef = true });
                if (compilation.HasErrors)
                {
                    var lines = File.ReadAllLines(targetFile);
                    foreach (var e in compilation.Diagnostics.Messages)
                    {
                        if (e.Type == CppLogMessageType.Error)
                        {
                            Console.WriteLine("Fixing " + targetFile + " removing line " + e.Location.Line);
                            RemoveAt(ref lines, e.Location.Line - 1);
                            File.WriteAllLines(targetFile, lines);
                            goto loopBreak;
                        }
                    }
                }
            loopBreak:;
            }
            while (compilation.HasErrors);

            return compilation;
        }

        static string FindType(string target, out int ptrLevel)
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
            Console.WriteLine(target + " " + new String('*', ptrLevel) + " :");
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


        static string BuildTempProject(string targetType, int bits, bool preprocessOnly = false)
        {
            string platform = bits == 32 ? "x86" : "x64";
            string debugFolder = bits == 32 ? "Debug" : "x64\\Debug";
            string temp = Path.GetTempPath();
            string slnDir = Path.Combine(temp, Guid.NewGuid().ToString());
            string cpp = Path.Combine(slnDir, "TempProject.cpp");
            string projFile = Path.Combine(slnDir, "TempProject.vcxproj");

            Directory.CreateDirectory(slnDir);
            CopyFile("Resources\\TempProject.cpp", slnDir);
            CopyFile("Resources\\TempProject.sln", slnDir);
            CopyFile("Resources\\TempProject.vcxproj", slnDir);
            CopyFile("Resources\\Dia2Dump.exe", slnDir);

            var txt = File.ReadAllText(cpp);
            txt = txt.Replace("//REPLACEME", targetType + " var;");
            File.WriteAllText(cpp, txt);

            if (preprocessOnly)
            {
                var txt2 = File.ReadAllText(projFile);
                txt2 = txt2.Replace("<!--REPLACEME-->", "<PreprocessToFile>true</PreprocessToFile>");
                File.WriteAllText(projFile, txt2);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            sb.Append("msbuild " + slnDir + "\\TempProject.sln /p:Configuration=Debug /p:Platform=\"" + platform + "\"");
            sb.Append(Environment.NewLine);
            if (!preprocessOnly)
            {
                sb.Append(@"if NOT [""%errorlevel%""]==[""0""] pause");
                sb.Append(Environment.NewLine);
                sb.Append("\"" + slnDir + "\\Dia2Dump.exe\" -all \"" + slnDir + "\\" + debugFolder + "\\TempProject.pdb\" > \"" + slnDir + "\\dump.txt\"");
                sb.Append(Environment.NewLine);
                sb.Append("cd \"" + slnDir + "\"");
                sb.Append(Environment.NewLine);
            }
            File.WriteAllText(Path.Combine(slnDir, "build.bat"), sb.ToString());
            RunCommand(Path.Combine(slnDir, "build.bat"));

            if (preprocessOnly)
                CopyFile(Path.Combine(slnDir, debugFolder, "TempProject.i"), slnDir);

            return slnDir;
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

        static void PrintTypeFrida(string target, int ptrLevel)
        {
            var t = Types[target];

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("function Dump" + target + "(ptr){");
            sb.AppendLine("  console.log('" + target + " at '+ptr);");
            foreach (var m in t)
            {
                sb.AppendLine("  console.log('" + m.Name + ": '+ " + TypeToString(m.Offset, m.Type) + ");        //" + m.Type);
            }
            sb.AppendLine("}");
            Console.WriteLine(sb.ToString());
        }

        static string TypeToString(int offset, string type)
        {
            if (type == "wchar_t *")
                return "ptr.add(" + offset + ").readUtf16String()";

            if (type.Contains("*"))
                return "ptr.add(" + offset + ").readPointer()";

            if (type == "int")
                return "'0x'+ptr.add(" + offset + ").readS32().toString(16)";

            if (type == "unsigned long")
                return "'0x'+ptr.add(" + offset + ").readU32().toString(16)";

            if (type == "unsigned short")
                return "'0x'+ptr.add(" + offset + ").readU16().toString(16)";

            if (type.StartsWith("char[") || type.StartsWith("unsigned char["))
            {
                var m = Regex.Match(type, @"[^\[]*\[0x([a-fA-F0-9]+)\]");
                return "ptr.add(" + offset + ").readByteArray(0x" + m.Groups[1].Value + ")";
            }

            return "ptr.add(" + offset + ").readPointer()";
        }

        public static void RemoveAt<T>(ref T[] arr, int index)
        {
            for (int a = index; a < arr.Length - 1; a++)
            {
                arr[a] = arr[a + 1];
            }
            Array.Resize(ref arr, arr.Length - 1);
        }
    }
}
