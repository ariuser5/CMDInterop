using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CMDInterop
{
	public class CmdPrompter
	{

		public enum OutputVerbosityTypes
		{
			Normal,
			Debug,
			Detailed
		}


		const int REFRESH_RATE = 100;
		const int MAX_EXEC_TIMEOUT = 120000;
		const int MIN_EXEC_TIMEOUT = 100;

		string _startLocation;
		bool _isPending;
		Process _cmdProcess;

		readonly string _pendingFlag;
		readonly Dictionary<string, string> _capturedVars;
		readonly Dictionary<object, OutputVerbosityTypes> _streams;

		public CmdPrompter()
		{
			this._isPending = false;
			this._pendingFlag = Guid.NewGuid().ToString();
			this._capturedVars = new Dictionary<string, string>();
			this._streams = new Dictionary<object, OutputVerbosityTypes>();
			this.IsPendingForCommand = false;

			this.ResetStartLocation();
		}


		public bool IsPendingForCommand { get; private set; }

		public bool IsOpened {
			get => !(this._cmdProcess?.HasExited) ?? false;
		}


		public string StartLocation {
			get => this._startLocation;
			set {
				if(Directory.Exists(value)) {
					this._startLocation = value;
				} else {
					throw new InvalidOperationException(
						"Invalid start location.");
				}
			}
		}

		public IEnumerable<string> CaptureVariables {
			get => this._capturedVars.Select(w => w.Key);
			set {
				var intersection = value.Intersect(this._capturedVars.Keys);

				foreach(var key in intersection) {
					if(!this._capturedVars.ContainsKey(key)) {
						this._capturedVars.Add(key, null);
					} else if(value.Contains(key) == false) {
						this._capturedVars.Remove(key);
					}
				}
			}
		}

		public IEnumerable<object> OutputStreams => this._streams.Keys;



		public void Start()
		{
			var procStartInfo = new ProcessStartInfo("cmd.exe")
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			this._cmdProcess = Process.Start(procStartInfo);

			this._cmdProcess.OutputDataReceived += EvaluateOutput;
			this._cmdProcess.BeginOutputReadLine();

			this._cmdProcess.ErrorDataReceived += EvaluateOutput;
			this._cmdProcess.BeginErrorReadLine();

			this._cmdProcess.StandardInput.WriteLine(this._pendingFlag);
			this.WaitForCommandFinish();
		}

		void EvaluateOutput(object sender, DataReceivedEventArgs e)
		{
			if(e.Data == this._pendingFlag) {
				this.IsPendingForCommand = true;
			}

			void WriteLineToOutputStream(object stream)
			{
				if(stream is StringBuilder sb) {

				} else
				if(stream is Stream str) {

				} else
				if(stream is TextWriter tr) {

				} else {
					throw new NotSupportedException();
				}
			}

			foreach(var stream in this._streams) {

				// TODO

				switch(stream.Value) {
					case OutputVerbosityTypes.Normal:
						WriteLineToOutputStream(stream);
						break;

					case OutputVerbosityTypes.Debug:
						WriteLineToOutputStream(stream);
						break;

					case OutputVerbosityTypes.Detailed:
						WriteLineToOutputStream(stream);
						break;
				}
			}
		}

		public void StartAt(string path)
		{
			this.StartLocation = path;
			this.Start();
		}

		public void Stop()
		{
			if(this.IsOpened) {
				if(this.IsPendingForCommand) {
					this._cmdProcess.StandardInput.WriteLine("exit");
					this.WaitForCommandFinish(MAX_EXEC_TIMEOUT);

					if(this.IsOpened) {
						this.Kill();
					}

				} else {
					throw new InvalidOperationException(
						"Cannot stop because a process is still executing.");
				}
			} else {
				throw new InvalidOperationException(
					"Cannot stop because the process isn't opened.");
			}
		}

		public void Kill()
		{
			if(this.IsOpened) {
				this._cmdProcess.Kill();
			} else {
				throw new InvalidOperationException(
					"Cannot kill because the process isn't opened.");
			}
		}

		public void Execute(string command, int timeout)
		{
			if(this.IsOpened) {
				if(timeout >= MIN_EXEC_TIMEOUT && timeout <= MAX_EXEC_TIMEOUT) {
					this.Execute(command);
					this.WaitForCommandFinish(timeout);
				} else {
					throw new InvalidOperationException(
						$"Argument '{nameof(timeout)}' cannot be " +
						$"smaller than {MIN_EXEC_TIMEOUT} or " +
						$"greater than {MAX_EXEC_TIMEOUT}.");
				}
			} else {
				throw new InvalidOperationException(
					"Cannot execute command because the process is not opened.");
			}
		}

		public void Execute(string command)
		{
			if(this.IsOpened) {
				if(this.IsPendingForCommand) {

					var myCmd = $"{command} & echo {this._pendingFlag}";

					this._cmdProcess.StandardInput.WriteLine(myCmd);
					this.IsPendingForCommand = false;

				} else {
					throw new InvalidOperationException(
						"Cannot execute command because " +
						"a process is still executing.");
				}
			} else {
				throw new InvalidOperationException(
					"Cannot execute command because the process is not opened.");
			}

			void RecordOutput(object sender, DataReceivedEventArgs e)
			{
				outputs.Add($"output>>{e.Data}");
				this._isPending |= e.Data == this._pendingFlag;
			}
		}

		public void WaitForCommandFinish(int waitTime = 0)
		{
			if(waitTime == 0) {
				while(this.IsOpened && this.IsPendingForCommand == false) {
					System.Threading.Thread.Sleep(REFRESH_RATE);
				}

			} else {
				if(waitTime >= MIN_EXEC_TIMEOUT && waitTime <= MAX_EXEC_TIMEOUT) {
					var count = 0;

					while(
						this.IsOpened && (
							this.IsPendingForCommand == false ||
							count < waitTime)) {

						System.Threading.Thread.Sleep(REFRESH_RATE);
						count += REFRESH_RATE;
					}

				} else {
					throw new InvalidOperationException(
						$"Argument '{nameof(waitTime)}' cannot be " +
						$"smaller than {MIN_EXEC_TIMEOUT} or " +
						$"greater than {MAX_EXEC_TIMEOUT}.");
				}
			}
		}

		public void ResetStartLocation()
		{
			var entryAsem = System.Reflection.Assembly
				.GetEntryAssembly().Location;

			this._startLocation = Path.GetDirectoryName(entryAsem);
		}

		public string ReadCapturedVariable(string name)
		{
			return this._capturedVars[name];
		}

		public void SetOutputStream(
			StringBuilder stringBuilder,
			OutputVerbosityTypes outputVerbosity = OutputVerbosityTypes.Normal)
		{

		}

		public void SetOutputStream(
			Stream stream,
			Encoding encoding,
			OutputVerbosityTypes outputVerbosity = OutputVerbosityTypes.Normal)
		{
			encoding.
		}

		public void SetOutputStream(
			TextWriter textWriter,
			OutputVerbosityTypes outputVerbosity = OutputVerbosityTypes.Normal)
		{

		}

	}
}
