using System;
using System.Diagnostics;
using System.IO;
using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IronFoundry.Warden.Containers
{
    public class ProcessLauncher
    {
        string hostExe = "IronFoundry.Warden.ContainerHost.exe";
        Process hostProcess;

        public Process LaunchProcess(ProcessStartInfo si, JobObject jobObject)
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

        private Process RequestStartProcess(ProcessStartInfo si)
        {
            var msg = new CreateProcessRequest(si);

            var jsonMessage = JsonConvert.SerializeObject(msg, Formatting.None);
            hostProcess.StandardInput.WriteLine(jsonMessage);

            var response = GetResponse<CreateProcessResponse>();
            return Process.GetProcessById(response.result.Id);
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
