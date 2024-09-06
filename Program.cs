using CommandLine;
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
        static void Main(string[] args)
        {
            DllExportEnumerator pe = new DllExportEnumerator();
            var exports = pe.GetExports(@"c:\Windows\System32\kernel32.dll");

            CppCompilation compilation = GetCompilation(32);

            TypeStore ts = new TypeStore();

            ts.LoadTypeDefs(compilation);

            int c = 0;
            foreach (var e in exports)
            {
                c++;
                CppFunction d = compilation.Functions.Where(i => string.Equals(i.Name, e, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (d == null)
                    Console.WriteLine("NO!!! " + e);
                else
                    ProcessFunction(compilation, d);

            }


            /*
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                   .WithParsed<CommandLineOptions>(o =>
                   {
                       if (o.Struct != null)
                           DumpStruct(o);

                       if (o.Function != null)
                           DumpFunction(o);

                   });
            */
        }

        static void ProcessFunction(CppCompilation compilation, CppFunction f)
        {
            Console.WriteLine(f.ToString());

            Console.Write(SimplifyType(f.ReturnType) + " " + f.Name + "(");

            int c = f.Parameters.Count;
            foreach (var p in f.Parameters)
            {
                Console.Write(SimplifyType(p.Type) + " " + p.Name);
                if (--c != 0)
                    Console.Write(",");
            }

            Console.WriteLine(")");
        }

        static string SimplifyType(CppType type)
        {
            CppType t = type;
            string str = "|";
            do
            {
                CppTypedef typedef = t as CppTypedef;
                CppPointerType pointer = t as CppPointerType;
                CppQualifiedType qua = t as CppQualifiedType;
                CppArrayType arr = t as CppArrayType;

                if (typedef != null)
                    t = typedef.ElementType;

                if (pointer != null)
                {
                    t = pointer.ElementType;
                    str += " *";
                }
                if (qua != null)
                {
                    t = qua.ElementType;
                    str = qua.Qualifier.ToString().ToLower()+" " + str;
                }
                if (arr != null)
                {
                    t = arr.ElementType;
                    str += "["+arr.Size+"]";
                }
            }
            while (t.TypeKind != CppTypeKind.Primitive && t.TypeKind != CppTypeKind.StructOrClass && t.TypeKind!= CppTypeKind.Function && t.TypeKind != CppTypeKind.Enum);

            if (t.TypeKind != CppTypeKind.Function)
            {
                CppFunctionType fun = t as CppFunctionType;
                //str = str.Replace("|", fun.ToString());
                str = type.ToString();
            }
            else if(t.TypeKind == CppTypeKind.StructOrClass)
            {
                CppClass c = t as CppClass;
                str = str.Replace("|", c.Name.ToString());
            }
            else if (t.TypeKind == CppTypeKind.StructOrClass)
            {
                CppEnum e = t as CppEnum;
                str = str.Replace("|", e.Name.ToString());
            }
            else
            {
                str = str.Replace("|", t.ToString());
            }

            return str;
        }

        static void DumpStruct(CommandLineOptions o)
        {
            try
            {
                string target = o.Struct;

                StructParser parser = new StructParser();

                parser.ProcessStruct(target, o.Bits);

                string realType = parser.FindType(target, out int ptrLevel);
                if (realType != null)
                {
                    if (o.Mode == "frida")
                        PrintTypeFrida(parser, realType, ptrLevel);
                    else
                        PrintType(parser, realType, ptrLevel);
                }
                else
                {
                    Console.WriteLine("Or try these:");

                    foreach (var t in parser.Types.Where(i => i.Key.Contains(target)))
                        Console.WriteLine(t.Value);

                    foreach (var t in parser.Typedef.Where(i => i.Key.Contains(target)))
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

            var d = compilation.Functions.Where(i => string.Equals(i.Name, o.Function, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            TypeStore st = new TypeStore();
            st.LoadFrom(compilation);

            if (d != null)
            {
                Console.WriteLine(d.ToString());
            }
            else
            {
                foreach (var f in compilation.Functions.Where(i => i.Name.StartsWith(o.Function, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine(f.ToString());
                }
            }
            return;
        }

        static CppCompilation GetCompilation(int bits)
        {
            Builder builder = new Builder();

            string slnPath = builder.BuildTempProject("int", bits, true);
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



        static void PrintType(StructParser parser, string target, int ptrLevel)
        {
            Console.WriteLine(target + " " + new String('*', ptrLevel) + " :");
            var t = parser.Types[target];
            foreach (var m in t)
            {
                Console.WriteLine("offset: 0x" + m.Offset.ToString("x") + " " + m.Type + " " + m.Name);
            }
        }




        static void PrintTypeFrida(StructParser parser, string target, int ptrLevel)
        {
            var t = parser.Types[target];

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
