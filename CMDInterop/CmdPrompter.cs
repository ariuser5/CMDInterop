﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
        const string VAR_QUERY_FLAG = "#:";
        const string CMD_CALL_PATTERN = "&(( *)?echo $(x)(&|$))";
        const string CALL_ECHO_PATTERN = "((.*?)>).+";

        string _startLocation;
        bool _isExecuting;
        bool _withholdProcess;
        bool _hideOutput;
        bool _isMultilineDefinitionContent;
        int _multilineDefinitionDepth;
        string _lastOutputLog;
        StringBuilder _lastCommandSb;
        Process _cmdProcess;

        readonly string _hiddenOutputFlag;
        readonly string _commandCompletedFlag;
        readonly CapturedVariableCollection _capturedVars;
        readonly Dictionary<object, OutputVerbosityTypes> _streams;

        public CmdPrompter()
        {
            this._commandCompletedFlag = Guid.NewGuid().ToString();
            this._hiddenOutputFlag = Guid.NewGuid().ToString();
            this._capturedVars = new CapturedVariableCollection();
            this._streams = new Dictionary<object, OutputVerbosityTypes>();
            this._lastCommandSb = new StringBuilder();

            this.ResetStartLocation();
        }



        string LastCommand {
            get => this._lastCommandSb.ToString() == string.Empty ?
                null :
                this._lastCommandSb.ToString();
        }

        public bool IsOpened {
            get => !(this._cmdProcess?.HasExited) ?? false;
        }

        public bool IsIdle {
            get => !this._isExecuting && !this._withholdProcess;
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

        public CapturedVariableCollection CapturedVariables => this._capturedVars;

        public IEnumerable<string> CaptureVariables {
            set {
                var union = value.Union(this._capturedVars);

                foreach(var key in union) {
                    if(value.Contains(key) &&
                       !this._capturedVars.Contains(key)) {

                        this._capturedVars.Add(key);
                    } else if(value.Contains(key) == false) {
                        this._capturedVars.Remove(key);
                    }
                }
            }
        }


        public IEnumerable<object> OutputStreams => this._streams.Keys;



        public void Start()
        {
            if(!this.IsOpened) {
                var procStartInfo = new ProcessStartInfo("cmd.exe")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = this.StartLocation
                };

                this._cmdProcess = Process.Start(procStartInfo);

                this._cmdProcess.OutputDataReceived += EvaluateOutput;
                this._cmdProcess.BeginOutputReadLine();

                this._cmdProcess.ErrorDataReceived += EvaluateOutput;
                this._cmdProcess.BeginErrorReadLine();

                this.ExecutePrivateCommand("");

                if(this.IsIdle == false) {
                    this.WaitForCommandFinish();
                }
            } else {
                throw new InvalidOperationException("Process is already opened");
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
                if(this.IsIdle) {

                    try {
                        this._cmdProcess.StandardInput.WriteLine("exit");
                        this.WaitForCommandFinish(MAX_EXEC_TIMEOUT);
                        this._cmdProcess.Close();
                        this._cmdProcess = null;
                    } finally {
                        if(this.IsOpened) {
                            this.Kill();
                        }
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

        public void Execute(string command)
        {
            if(this.IsOpened) {
                this.ExecuteAsync(command);
                this.WaitForCommandFinish();
            } else {
                throw new InvalidOperationException(
                    "Cannot execute command because the process is not opened.");
            }
        }

        public void Execute(string command, int timeout)
        {
            if(this.IsOpened) {
                if(timeout >= MIN_EXEC_TIMEOUT && timeout <= MAX_EXEC_TIMEOUT) {
                    this.ExecuteAsync(command);
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

        public void ExecuteAsync(string command)
        {
            if(this.IsOpened) {
                if(this.IsIdle) {
                    var managedCmd = $"{command} & echo {this._commandCompletedFlag}";
                    var postExecVariableCapturing = new Queue<Action>();

                    this._isExecuting = true;
                    this._withholdProcess = true;
                    this._cmdProcess.StandardInput.WriteLine(managedCmd);

                    foreach(var captureVar in this.CapturedVariables) {
                        var captureCommand =
                            $"echo {VAR_QUERY_FLAG}{captureVar}=%{captureVar}%";

                        void CaptureVariable()
                            => this.ExecutePrivateCommand(captureCommand);

                        postExecVariableCapturing.Enqueue(CaptureVariable);
                    }

                    this.AwaitAndDoPostExecutionTasks(postExecVariableCapturing);

                } else {
                    throw new InvalidOperationException(
                        "Cannot execute command because " +
                        "a process is still executing.");
                }
            } else {
                throw new InvalidOperationException(
                    "Cannot execute command because the process is not opened.");
            }
        }

        public void WaitForCommandFinish(int waitTime = 0)
        {
            if(waitTime == 0) {
                while(this.IsOpened && this.IsIdle == false) {
                    System.Threading.Thread.Sleep(REFRESH_RATE);
                }

            } else {
                if(waitTime >= MIN_EXEC_TIMEOUT && waitTime <= MAX_EXEC_TIMEOUT) {
                    var count = 0;

                    while(
                        this.IsOpened && (
                            this.IsIdle == false &&
                            count <= waitTime)) {

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

            if(this.IsIdle == false) {
                throw new TimeoutException();
            }
        }

        public void ResetStartLocation()
        {
            var entryAsem = System.Reflection.Assembly
                .GetEntryAssembly().Location;

            this._startLocation = Path.GetDirectoryName(entryAsem);

            if(this.IsOpened) {
                if(this.IsIdle) {
                    this.ExecutePrivateCommand($"cd \"{this._startLocation}\"");
                } else {
                    throw new InvalidOperationException(
                        "Cannot change start location because " +
                        "a process is still executing.");
                }
            }
        }

        public void SetOutputStream(
            StringBuilder stringBuilder,
            OutputVerbosityTypes outputVerbosity = OutputVerbosityTypes.Normal)
        {
            this._streams.Add(stringBuilder, outputVerbosity);
        }

        public void SetOutputStream(
            TextWriter textWriter,
            OutputVerbosityTypes outputVerbosity = OutputVerbosityTypes.Normal)
        {
            this._streams.Add(textWriter, outputVerbosity);
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encoding"></param>
        /// <param name="outputVerbosity"></param>
        public void SetOutputStream(
            Stream stream,
            Encoding encoding,
            OutputVerbosityTypes outputVerbosity = OutputVerbosityTypes.Normal)
        {
            throw new NotImplementedException();
        }

        public bool RemoveOutputStream(StringBuilder stringBuilder)
        {
            return this._RemoveOutputStream(stringBuilder);
        }

        public bool RemoveOutputStream(TextWriter textWriter)
        {
            return this._RemoveOutputStream(textWriter);
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="stringBuilder"></param>
        /// <returns></returns>
        public bool RemoveOutputStream(Stream stream)
        {
            throw new NotImplementedException();
        }

        bool _RemoveOutputStream(object stream)
        {
            if(this._streams.ContainsKey(stream)) {
                this._streams.Remove(stream);
                return true;
            } else {
                return false;
            }
        }

        void ExecutePrivateCommand(string command)
        {
            var managedCmd =
                $"echo {this._commandCompletedFlag}" +
                $"& echo {this._hiddenOutputFlag}";

            if(!string.IsNullOrEmpty(command)) {
                managedCmd = $"{command}& {managedCmd}";
            }

            this._isExecuting = true;
            this._cmdProcess.StandardInput.WriteLine(managedCmd);
        }

        void EvaluateOutput(object sender, DataReceivedEventArgs e)
        {
            var ranToCompletion = !this._isExecuting;
            var isCommand = false;

            if(e.Data == this._commandCompletedFlag) {
                ranToCompletion = true;
            } else if(e.Data != null) {
                isCommand = this.IsCommand(e.Data);

                if(isCommand) {
                    this._lastCommandSb = new StringBuilder(e.Data);
                }

                if(e.Data.EndsWith(this._hiddenOutputFlag)) {
                    this._hideOutput = true;
                } else if(
                    e.Data == string.Empty &&
                    this._lastOutputLog == this._hiddenOutputFlag) {

                    this._hideOutput = false;
                }

                this.UpdateVariableIfCapturing(e.Data);
                this.ParseForMultilineDefinition(e.Data);

                if(this._multilineDefinitionDepth > 0) {
                    this._isMultilineDefinitionContent = true;
                }

                if(this._isMultilineDefinitionContent && isCommand == false) {
                    this._lastCommandSb.AppendLine(e.Data);
                }

                this.WriteLogToStreams(e.Data);

                if(this._multilineDefinitionDepth == 0) {
                    this._isMultilineDefinitionContent = false;
                }
            }

            this._lastOutputLog = e.Data;
            this._isExecuting = !ranToCompletion;
        }

        bool IsCommand(string logMessage)
        {
            var callEchoMatch = Regex.Match(logMessage, CALL_ECHO_PATTERN);

            if(callEchoMatch.Success &&
               Directory.Exists(callEchoMatch.Groups[2].Value)) {

                return true;
            }

            return false;
        }

        void UpdateVariableIfCapturing(string logMessage)
        {
            var varQueryPattern = $"^{VAR_QUERY_FLAG}.+=";
            var match = Regex.Match(logMessage, varQueryPattern);

            if(match.Success) {
                var varName = match.Value
                    .Replace(VAR_QUERY_FLAG, string.Empty)
                    .Replace("=", string.Empty);
                var varValue = logMessage.Replace(match.Value, string.Empty);

                this._capturedVars[varName] = varValue;
            }
        }

        void WriteLogToStreams(string logMessage)
        {
            void WriteTo(object stream)
                => this.CallAppropriateWriteLineMethodForStream(stream, logMessage);

            foreach(var streamKvp in this._streams) {

                if(streamKvp.Value == OutputVerbosityTypes.Detailed) {
                    WriteTo(streamKvp.Key);
                } else {

                    if(this._hideOutput) {
                        return;
                    } else if(logMessage == this._commandCompletedFlag) {
                        continue;
                    } else if(logMessage == this._hiddenOutputFlag) {
                        this._hideOutput = false;
                        continue;
                    } else {

                        var cmdCallPattern = CMD_CALL_PATTERN.Replace(
                            oldValue: "$(x)",
                            newValue: this._commandCompletedFlag);
                        var cmdCallMatch = Regex.Match(logMessage, cmdCallPattern);

                        if(cmdCallMatch.Success) {
                            logMessage = logMessage.Replace(
                                oldValue: cmdCallMatch.Value,
                                newValue: string.Empty);
                        }
                    }

                    if(streamKvp.Value == OutputVerbosityTypes.Normal) {

                        if(this._isMultilineDefinitionContent ||
                           logMessage == string.Empty && this.LastCommand != null) {

                            continue;
                        } else {
                            var cmdEchoMatch = Regex.Match(logMessage, CALL_ECHO_PATTERN);

                            if(cmdEchoMatch.Success == false) {
                                WriteTo(streamKvp.Key);
                            }
                        }

                    } else {
                        WriteTo(streamKvp.Key);
                    }

                }

            }
        }

        void CallAppropriateWriteLineMethodForStream(object stream, string outputLog)
        {
            if(stream is StringBuilder sb) {
                sb.AppendLine(outputLog);
            } else
            if(stream is TextWriter tr) {
                tr.WriteLine(outputLog);
            } else
            if(stream is Stream str) {
                throw new NotImplementedException();
            } else {
                throw new NotSupportedException();
            }
        }

        void AwaitAndDoPostExecutionTasks(Queue<Action> captureVariableTasks)
        {
            var awaiter = new Thread(WaitForCommandToFinishAndDoTasks);
            awaiter.Start();

            void WaitForCommandToFinishAndDoTasks()
            {
                while(captureVariableTasks.Any()) {
                    var currCapturing = captureVariableTasks.Dequeue();

                    while(this.IsOpened && this._isExecuting) {
                        Thread.Sleep(REFRESH_RATE);
                    }

                    currCapturing.Invoke();
                }

                this._withholdProcess = false;
            }
        }

        void ParseForMultilineDefinition(string outputLog)
        {
            CharEnumerator iterator;
            char lastChar = default;

            var definition = outputLog;
            var cmdEchoMatch = Regex.Match(outputLog, CALL_ECHO_PATTERN);

            if(cmdEchoMatch.Success) {
                definition = outputLog.Replace(
                    oldValue: cmdEchoMatch.Groups[1].Value,
                    newValue: string.Empty);
            }

            iterator = definition.GetEnumerator();

            while(iterator.MoveNext()) {
                if(iterator.Current == '(' && lastChar != '^') {
                    this._multilineDefinitionDepth++;
                } else if(iterator.Current == ')' && lastChar != '^') {
                    this._multilineDefinitionDepth--;
                }

                lastChar = iterator.Current;
            }

        }


    }
}
