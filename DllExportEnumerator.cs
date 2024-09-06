using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinApiMapper
{
    class DllExportEnumerator
    {
        public List<string> GetExports(string dllFileName)
        {
            List<string> lines = new List<string>();


            string output = string.Empty;
            var info = new ProcessStartInfo();
            var process = new Process();

            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.FileName = "dumpbin.exe";

            info.Arguments = string.Format("/exports \"{0}\"", dllFileName);

            process.OutputDataReceived += (senderObject, args) => lines.Add(args.Data);
            process.StartInfo = info;
            process.Start();
            process.BeginOutputReadLine();

            process.WaitForExit();

            List<string> result = new List<string>();
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var m = Regex.Match(line, @"\s+\d+\s+[0-9a-fA-F]*\s+[0-9a-fA-F]+\s(.*)");
                if (m.Success && !m.Groups[1].Value.Contains("("))
                    result.Add(m.Groups[1].Value);
            }

            return result;
        }
    }
}
