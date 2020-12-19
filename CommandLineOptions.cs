using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinApiMapper
{
    public class CommandLineOptions
    {
        [Option('b', "bits", Required = true, HelpText = "32 or 64")]
        public int Bits { get; set; }

        [Option('f', "function", Required = false, HelpText = "Name of function")]
        public string Function { get; set; }

        [Option('s', "struct", Required = false, HelpText = "Name of struct")]
        public string Struct { get; set; }

        [Option('m', "mode", Required = false, HelpText = "Mode (use frida)")]
        public string Mode { get; set; }
    }
}
