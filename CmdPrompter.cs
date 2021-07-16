using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CMDInterop
{
    public class CmdPrompter
    {

        string _startLocation;
        bool _isPending;
        Process _cmdProcess;

        readonly string _ranToCompletionFlag = Guid.NewGuid().ToString();
        readonly Dictionary<string, string> _capturedVars;

        public CmdPrompter()
        {
            var execAsemLocation =

            this._isPending = false;
            this._ranToCompletionFlag = Guid.NewGuid().ToString();
            this._capturedVars = new Dictionary<string, string>();
        }


        public bool IsOpened {
            get => !(this._cmdProcess?.HasExited) ?? false;
        }

        public bool IsPending {
            get { }
        }

        public string StartLocation {
            get => this._startLocation;
            set;
        }

        public IEnumerable<string> CaptureVariables {
            get => this._capturedVars.Select(w => w.Key);
            set {
                var intersection = value.Intersect(this._capturedVars.Keys);

                foreach(var key in intersection) {
                    var exists = this._capturedVars.TryGetValue(key, out var result);

                    if(!exists) {
                        this._capturedVars.Add(key, null);
                    } else {
                        this._capturedVars.Remove(key);
                    }
                }
            }
        }



        public void Start()
        {

        }

        public void StartAt(string path)
        {

        }

        public void Stop()
        {
            if(this.IsOpened) {



                this._cmdProcess.StandardInput.WriteLine("exit");
            }
        }

        public void Kill()
        {
            // 
        }

        public void Execute(string command, int timeout)
        {

        }

        public void Execute(string command)
        {
            var myCmd = command.Equals(string.Empty) ?
                $"echo {retvar}=%{retvar}%" :
                $"{command} & call echo {this._ranToCompletionFlag}";
            var processInfo = new ProcessStartInfo("cmd.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(processInfo);
            var outputs = new List<string>();

            process.StandardInput.WriteLine(myCmd);

            void RecordOutput(object sender, DataReceivedEventArgs e)
            {
                outputs.Add($"output>>{e.Data}");
                this._isPending |= e.Data == this._ranToCompletionFlag;
            }

            process.OutputDataReceived += RecordOutput;
            process.BeginOutputReadLine();

            process.ErrorDataReceived += RecordOutput;
            process.BeginErrorReadLine();

            System.Threading.Thread.Sleep(2000);

            process.OutputDataReceived -= RecordOutput;
            process.StandardInput.WriteLine("echo 1");
            process.StandardInput.WriteLine("echo 2");
            process.StandardInput.WriteLine("echo 3");
            process.OutputDataReceived += RecordOutput;

            while(!this._isPending) {
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

        public string ReadVariable(string name)
        {
            return this._capturedVars[name];
        }

    }
}
