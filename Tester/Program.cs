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
			var prompter = new CmdPrompter
			{
				StartLocation = "scripts",
				CaptureVariables = new[] { "vs_folder", "PATH", "myvar" }
			};

			prompter.SetOutputStream(Console.Out, CmdPrompter.OutputVerbosityTypes.Detailed);
			prompter.Start();

			try {
				prompter.Execute("vs_dirset", 3000);
			} catch(Exception ex) {
				throw new Exception("", ex);
			}

			var result = prompter.ReadCapturedVariable("vs_folder");

			Console.ReadKey();
		}
	}
}
