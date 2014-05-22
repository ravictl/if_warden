﻿using System.Security;
using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json.Linq;

namespace IronFoundry.Warden.Containers.Messages
{
    public class ContainerInitializeParameters
    {
        public string containerHandle;
        public string containerBaseDirectoryPath;
    }

    public class ContainerInitializeRequest : JsonRpcRequest<ContainerInitializeParameters>
    {
        public static string MethodName = "ContainerInitialize";
        public ContainerInitializeRequest(ContainerInitializeParameters messageParams) : base(MethodName)
        {
            @params = messageParams;
        }
    }

    public class ContainerInitializeResponse : JsonRpcResponse<string>
    {
        public ContainerInitializeResponse(JToken id, string containerPath) : base(id, containerPath)
        {
        }
    }
}
