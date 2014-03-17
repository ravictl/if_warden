using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using IronFoundry.Warden.Shared.Messaging;
using IronFoundry.Warden.Utilities;

namespace IronFoundry.Warden.Containers
{
    public class ProcessLauncher
    {
        string hostExe = "IronFoundry.Warden.ContainerHost.exe";
        Process hostProcess;
        MessageTransport messageTransport;
        MessagingClient messagingClient;

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

                messageTransport = new MessageTransport(hostProcess.StandardOutput, hostProcess.StandardInput);
                messagingClient = new MessagingClient(message => 
                {
                    messageTransport.PublishAsync(message).GetAwaiter().GetResult();
                });
                messageTransport.SubscribeResponse(message =>
                {
                    messagingClient.PublishResponse(message);
                    return Task.FromResult(0);
                });

                jobObject.AssignProcessToJob(hostProcess);
            }

            return RequestStartProcessAsync(si)
                .GetAwaiter()
                .GetResult();
        }

        private async Task<IProcess> RequestStartProcessAsync(ProcessStartInfo si)
        {
            CreateProcessRequest request = new CreateProcessRequest(si);
            CreateProcessResponse response = null;
            
            try
            {
                response = await messagingClient.SendMessageAsync<CreateProcessRequest, CreateProcessResponse>(request);
            }
            catch (MessagingException ex)
            {
                throw ProcessLauncherError(ex);
            }

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
                var exitInfo = await GetProcessExitInfoAsync(response.result.Id);
                var message = String.Format("Process was unable to start or died prematurely. Process exit code was {0}.\n{1}", exitInfo.ExitCode, exitInfo.StandardError);

                throw ProcessLauncherError(message, exitInfo.ExitCode, exitInfo.StandardError);
            }

            return new RealProcessWrapper(process);
        }

        private async Task<GetProcessExitInfoResult> GetProcessExitInfoAsync(int processId)
        {
            GetProcessExitInfoRequest request = new GetProcessExitInfoRequest(
                new GetProcessExitInfoParams
                {
                    Id = processId
                });
            GetProcessExitInfoResponse response = null;

            try
            {
                response = await messagingClient.SendMessageAsync<GetProcessExitInfoRequest, GetProcessExitInfoResponse>(request);
            }
            catch (MessagingException ex)
            {
                throw ProcessLauncherError(ex);
            }

            return response.result;
        }

        private ProcessLauncherException ProcessLauncherError(string message, int code, string remoteData, Exception innerException = null)
        {
            return new ProcessLauncherException(message, innerException)
            {
                Code = code,
                RemoteData = remoteData,
            };
        }

        private ProcessLauncherException ProcessLauncherError(MessagingException ex)
        {
            var errorInfo = ex.ErrorResponse.error;
            return ProcessLauncherError(errorInfo.Message, errorInfo.Code, errorInfo.Data, ex);
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
