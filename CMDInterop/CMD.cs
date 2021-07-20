using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CMDInterop
{

	[Obsolete]
	public static class CMD
	{

		const string BATCH_COMPLETION_FLAG = "_completed_";
		const int WAIT_MILLIS = 500;


		public static string RunCommand(string command, string retvar = null)
		{
			var myCmd = command.Equals(string.Empty) ?
				$"echo {retvar}=%{retvar}%" :
				$"{command} & call echo {BATCH_COMPLETION_FLAG}";
			var processInfo = new ProcessStartInfo("cmd.exe")
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			var process = Process.Start(processInfo);
			var batchRanToCompletion = false;
			var outputs = new List<string>();

			process.StandardInput.WriteLine(myCmd);

			void RecordOutput(object sender, DataReceivedEventArgs e)
			{
				outputs.Add($"output>>{e.Data}");
				batchRanToCompletion |= e.Data == BATCH_COMPLETION_FLAG;
			}

			process.OutputDataReceived += RecordOutput;
			process.BeginOutputReadLine();

			process.ErrorDataReceived += RecordOutput;
			process.BeginErrorReadLine();

			while(!batchRanToCompletion) {
				System.Threading.Thread.Sleep(WAIT_MILLIS);
			}

			if(string.IsNullOrEmpty(retvar) == false) {
				var outputsCount = outputs.Count;

				process.StandardInput.WriteLine($"echo {retvar}=%{retvar}%");

				while(outputsCount == outputs.Count) {
					System.Threading.Thread.Sleep(WAIT_MILLIS);
				}

				retvar = outputs
					.LastOrDefault(s => s.StartsWith($"output>>{retvar}="))?
					.Replace($"output>>{retvar}=", string.Empty);
			}

			process.StandardInput.WriteLine("exit");
			process.WaitForExit();

			Console.WriteLine("ExitCode: {0}", process.ExitCode);
			process.Close();

			return retvar;
		}

	}
}
