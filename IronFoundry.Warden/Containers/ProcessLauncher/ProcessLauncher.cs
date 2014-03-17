using System;
using System.Diagnostics;
using System.IO;
using IronFoundry.Warden.Shared.Messaging;
using IronFoundry.Warden.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IronFoundry.Warden.Containers
{
    public class ProcessLauncher
    {
        string hostExe = "IronFoundry.Warden.ContainerHost.exe";
        Process hostProcess;

        public IProcess LaunchProcess(ProcessStartInfo si, JobObject jobObject)
        {
            if (hostProcess == null)
            {
                var hostFullPath = Path.Combine(Directory.GetCurrentDirectory(), hostExe);
                var hostStartInfo = new ProcessStartInfo(hostFullPath);
                hostStartInfo.RedirectStandardInput = true;
                hostStartInfo.RedirectStandardOutput = true;
                hostStartInfo.RedirectStandardError = true;
                hostStartInfo.UseShellExecute = false;

                hostProcess = Process.Start(hostStartInfo);

                jobObject.AssignProcessToJob(hostProcess);
            }

            return RequestStartProcess(si);
        }

        private IProcess RequestStartProcess(ProcessStartInfo si)
        {
            var msg = new CreateProcessRequest(si);

            var jsonMessage = JsonConvert.SerializeObject(msg, Formatting.None);
            hostProcess.StandardInput.WriteLine(jsonMessage);

            var response = GetResponse<CreateProcessResponse>();
            Process process = null;
            try
            {
                process = Process.GetProcessById(response.result.Id);
            }
            catch (ArgumentException)
            {
            }

            if (process == null)
            {
                // The process was unable to start or has died prematurely
                var exitInfo = GetProcessExitInfo(response.result.Id);
                var message = String.Format("Process was unable to start or died prematurely. Process exit code was {0}.\n{1}", exitInfo.ExitCode, exitInfo.StandardError);

                throw new ProcessLauncherException(message)
                {
                    Code = exitInfo.ExitCode,
                    RemoteData = exitInfo.StandardError,
                };
            }

            return new RealProcessWrapper(process);
        }

        private GetProcessExitInfoResult GetProcessExitInfo(int processId)
        {
            var request = new GetProcessExitInfoRequest(
                new GetProcessExitInfoParams
                {
                    Id = processId
                });

            var jsonRequest = JsonConvert.SerializeObject(request, Formatting.None);
            hostProcess.StandardInput.WriteLine(jsonRequest);

            var jsonResponse = GetResponse<GetProcessExitInfoResponse>();
            return jsonResponse.result;
        }

        private JObject GetResponse()
        {
            var response = hostProcess.StandardOutput.ReadLine();
            return JObject.Parse(response);
        }

        private T GetResponse<T>()
            where T : JsonRpcResponse, new()
        {
            var response = GetResponse();

            var error = response["error"];
            if (error != null)
            {
                throw new ProcessLauncherException(error["message"].ToString()) { Code = (int)error["code"], RemoteData = error["data"].ToString() };
            }

            return response.ToObject<T>();
        }

        class RealProcessWrapper : IProcess
        {
            private readonly Process process;
            public event EventHandler Exited;

            public RealProcessWrapper(Process process)
            {
                this.process = process;
                Id = process.Id;
                process.Exited += (o, e) => this.OnExited();
            }

            public int Id { get; private set; }

            public int ExitCode
            {
                get { return process.ExitCode; }
            }

            public IntPtr Handle
            {
                get { return process.Handle; }
            }

            public bool HasExited
            {
                get { return process.HasExited; }
            }

            public TimeSpan TotalProcessorTime
            {
                get { return process.TotalProcessorTime; }
            }

            public TimeSpan TotalUserProcessorTime
            {
                get { return process.UserProcessorTime; }
            }

            public void Kill()
            {
                process.Kill();
            }

            protected virtual void OnExited()
            {
                var handlers = Exited;
                if (handlers != null)
                {
                    handlers.Invoke(this, EventArgs.Empty);
                }
            }

            public long WorkingSet
            {
                get { return process.WorkingSet64; }
            }

            public void Dispose()
            {
                process.Dispose();
            }
        }
    }

    [Serializable]
    public class ProcessLauncherException : Exception
    {
        public ProcessLauncherException() { }
        public ProcessLauncherException(string message) : base(message) { }
        public ProcessLauncherException(string message, Exception inner) : base(message, inner) { }
        protected ProcessLauncherException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }

        public int Code { get; set; }
        public string RemoteData { get; set; }
    }
}
