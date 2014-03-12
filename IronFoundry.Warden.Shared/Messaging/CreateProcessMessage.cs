using System.Diagnostics;

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
                Arguments = this.Arguments,
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
