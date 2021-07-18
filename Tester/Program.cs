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
				CaptureVariables = new[] { "PATH" }
			};

			prompter.SetOutputStream(Console.Out);
			prompter.Start();
			prompter.Execute("vs_dirset");
			prompter.WaitForCommandFinish();

			var result = prompter.ReadCapturedVariable("PATH");

			Console.ReadKey();
		}
	}
}
