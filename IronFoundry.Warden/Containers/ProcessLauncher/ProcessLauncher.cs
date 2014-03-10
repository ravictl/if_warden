using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronFoundry.Warden.Containers
{
    public class ProcessLauncher
    {
        string hostExe = "IronFoundry.Warden.ContainerHost.exe";
        Process hostProcess;

        public Process LaunchProcess(System.Diagnostics.ProcessStartInfo si, JobObject jobObject)
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

            Process p = RequestStartProcess(si);

            return p;
        }

        private Process RequestStartProcess(ProcessStartInfo si)
        {

            var msg = new CreateProcessMessage(si);

            var jsonMessage = JsonConvert.SerializeObject(msg, Formatting.None);
            hostProcess.StandardInput.WriteLine(jsonMessage);

            var response = GetResponse();

            var error = response["error"];
            if (error != null)
            {
                throw new ProcessLauncherException(error["message"].ToString()) { Code = (int)error["code"], RemoteData = error["data"].ToString() };
            }

            int processId = (int)response["result"];
            return Process.GetProcessById(processId);

        }

        private JObject GetResponse()
        {
            var response = hostProcess.StandardOutput.ReadLine();
            return JObject.Parse(response);
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
