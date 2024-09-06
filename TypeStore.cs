using CppAst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinApiMapper
{
    class MapperType
    {
        public string Name;
        public string TypeDefinition;
        public string FullDefinition;
        public string PointedType;
        public bool IsBasicType;
        public bool IsConst;
        public bool IsArray;
        public bool IsPointer;
        public int PointerDepth;
        public bool IsUnsigned;
        public bool IsEnum;
        public bool IsStruct;
        public bool Orphan;
        public bool Typedef;
    }

    class TypeStore
    {
        Dictionary<string, MapperType> KnownTypes = new Dictionary<string, MapperType>();

        public TypeStore()
        {
            foreach (string s in new[] { "char", "int", "short", "long", "__int64","long long" })
            {
                KnownTypes.Add(s, new MapperType() { Name = s, IsBasicType = true, IsUnsigned = false });
                KnownTypes.Add("unsigned " + s, new MapperType() { Name = "unsigned " + s, IsBasicType = true, IsUnsigned = false });
                KnownTypes.Add(s + "*", new MapperType() { Name = s + "*", IsBasicType = true, IsUnsigned = false, IsPointer = true });
                KnownTypes.Add("unsigned " + s + "*", new MapperType() { Name = "unsigned " + s + "*", IsBasicType = true, IsUnsigned = false, IsPointer = true });
            }
            KnownTypes.Add("bool", new MapperType() { Name = "bool", IsBasicType = true, IsUnsigned = false });
            KnownTypes.Add("float", new MapperType() { Name = "bool", IsBasicType = true, IsUnsigned = false });
            KnownTypes.Add("double", new MapperType() { Name = "bool", IsBasicType = true, IsUnsigned = false });
            KnownTypes.Add("void", new MapperType() { Name = "void", IsBasicType = true, IsUnsigned = false });
            KnownTypes.Add("void*", new MapperType() { Name = "void*", IsBasicType = true, IsUnsigned = false, IsPointer = true });
        }


        public void LoadFrom(CppCompilation compilation)
        {
            foreach (var s in compilation.Classes.Where(i=>i.Name== "_TP_CALLBACK_ENVIRON_V3"))
                Console.WriteLine(s);

            LoadTypeDefs(compilation);
        }




        public void LoadTypeDefs(CppCompilation compilation)
        {
            foreach (var t in compilation.Typedefs)
            {
                if (KnownTypes.ContainsKey(t.Name))
                    continue;

                MapperType mt = new MapperType();
                mt.Name = t.Name;
                mt.Typedef = true;

                mt.IsArray = t.ElementType.TypeKind == CppTypeKind.Array;
                mt.IsEnum = t.ElementType.TypeKind == CppTypeKind.Enum;
                mt.IsStruct = t.ElementType.TypeKind == CppTypeKind.StructOrClass;

                string str = t.ToString();

                mt.FullDefinition = str;

                if (str.EndsWith(t.Name))
                    str = str.Substring(0, str.Length - t.Name.Length);
                else
                    Console.WriteLine("Error1");

                if (str.StartsWith("typedef "))
                    str = str.Substring(8);
                else
                    Console.WriteLine("Error2");

                str = str.Trim();
                mt.TypeDefinition = str;

                while(str.EndsWith("*"))
                {
                    mt.IsPointer = true;
                    mt.PointerDepth++;
                    str = str.Substring(0, str.Length - 1).Trim();
                }

                if(mt.IsPointer != (t.ElementType.TypeKind == CppTypeKind.Pointer))
                    Console.WriteLine("Error3");

                if (str.StartsWith("const "))
                {
                    str = str.Substring(6);
                    mt.IsConst = true;
                }

                mt.PointedType = str;

                if (!KnownTypes.ContainsKey(str) && !mt.IsEnum && !mt.IsStruct)
                {
                    mt.Orphan = true;
                    Console.WriteLine(str);
                }
                 

                KnownTypes.Add(t.Name, mt);
            }

            foreach(var x in KnownTypes)
            {
                if (x.Value.Orphan && KnownTypes.ContainsKey(x.Value.PointedType))
                    x.Value.Orphan = false;
            }

            foreach (var x in KnownTypes)
            {
                if (x.Value.Orphan)
                    Console.WriteLine("Orphan:"+ x.Value.FullDefinition);
            }
        }
    }
}
