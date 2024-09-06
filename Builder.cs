using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinApiMapper
{
    class Builder
    {
        public string BuildTempProject(string targetType, int bits, bool preprocessOnly = false)
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

        public void CopyFile(string name, string dstFolder)
        {
            File.Copy(name, Path.Combine(dstFolder, Path.GetFileName(name)));
        }
        public void RunCommand(string command)
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
