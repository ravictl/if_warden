using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronFoundry.Warden.Shared.Messaging
{
    public class CreateProcessMessage : JsonRpcRequest<CreateProcessParams>
    {
        public CreateProcessMessage() 
            : base("CreateProcess")
        {
        }

        public CreateProcessMessage(ProcessStartInfo startInfo)
            : base("CreateProcess")
        {
            @params = new CreateProcessParams(startInfo);
        }
    }

    public class CreateProcessParams 
    {
        public CreateProcessParams()
        {
        }

        public CreateProcessParams(ProcessStartInfo si)
        {
            this.FileName = si.FileName;
            this.Arguments = si.Arguments;
        }

        public string FileName { get; set; }
        public string Arguments { get; set; }

        public ProcessStartInfo ToProcessStartInfo()
        {
            return new ProcessStartInfo() 
            { 
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = this.FileName, 
                Arguments = this.Arguments 
            };
        }
    }
}
