using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;

namespace IronFoundry.Warden.Shared.Messaging
{
    public class CreateProcessParams
    {
        public CreateProcessParams()
        {
        }

        public CreateProcessParams(ProcessStartInfo si)
        {
            this.FileName = si.FileName;
            this.Arguments = si.Arguments;
            this.UserName = si.UserName;
            this.Password = si.Password == null ? null : si.Password.ToUnsecureString();
        }

        public string FileName { get; set; }
        public string Arguments { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public ProcessStartInfo ToProcessStartInfo()
        {
            return new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = this.FileName,
                Arguments = this.Arguments,
                UserName = this.UserName,
                Password = string.IsNullOrEmpty(this.Password) ? null : this.Password.ToSecureString(),
            };
        }
    }

    public class CreateProcessRequest : JsonRpcRequest<CreateProcessParams>
    {
        public CreateProcessRequest() 
            : base("CreateProcess")
        {
        }

        public CreateProcessRequest(ProcessStartInfo startInfo)
            : base("CreateProcess")
        {
            @params = new CreateProcessParams(startInfo);
        }
    }
    
    public class CreateProcessResult
    {
        public int Id { get; set; }
    }

    public class CreateProcessResponse : JsonRpcResponse<CreateProcessResult>
    {
        public CreateProcessResponse() : base()
        {
        }

        public CreateProcessResponse(string id, CreateProcessResult result) : base(id, result)
        {
        }
    }
}
