using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronFoundry.Warden.Shared.Messaging
{
    public class JsonRpcResponse
    {
        public JsonRpcResponse(string id)
        {
            this.id = id;
        }

        public string jsonrpc { get { return "2.0"; } }
        public string id { get; private set; }
    }

    public class JsonRpcResponse<TResult> : JsonRpcResponse
    {
        public JsonRpcResponse(string id, TResult result) : base(id)
        {
            this.result = result;
        }

        public TResult result { get; private set; }
    }

    
    public class JsonRpcErrorInfo 
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public string Data { get; set; }
    }

    public class JsonRpcErrorResponse : JsonRpcResponse
    {
        public JsonRpcErrorResponse(string id) : base (id)
        {
            error = new JsonRpcErrorInfo();
        }

        public JsonRpcErrorInfo error { get; private set; }
    }

}
