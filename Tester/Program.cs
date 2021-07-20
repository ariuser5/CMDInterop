using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CMDInterop;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            TestOpenAndReopen();
            Console.ReadKey();
        }

        static void TestOpenAndReopen()
        {
            var prompter = new CmdPrompter
            {
                StartLocation = "scripts",
                CaptureVariables = new[] { "PATH" }
            };

            prompter.SetOutputStream(Console.Out, CmdPrompter.OutputVerbosityTypes.Detailed);
            prompter.Start();

            try {
                prompter.Execute("vs_dirset");
            } catch(Exception ex) {
                throw new Exception("", ex);
            }

            prompter.Stop();

            var result = prompter.CapturedVariables["PATH"];
            prompter.CapturedVariables["PATH"] = null;
            result = prompter.CapturedVariables["PATH"];

            prompter.Start();

            try {
                prompter.Execute("vs_dirset");
            } catch(Exception ex) {
                throw new Exception("", ex);
            }

            result = prompter.CapturedVariables["PATH"];
        }

    }
}
