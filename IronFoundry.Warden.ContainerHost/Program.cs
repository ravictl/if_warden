using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronFoundry.Warden.ContainerHost
{
    class Program
    {
        static void Main(string[] args)
        {
            MessageDispatcher dispatcher = new MessageDispatcher();
            dispatcher.RegisterMethod<CreateProcessRequest>("CreateProcess", CreateProcessHandler);

            bool exit = false;
            while (!exit)
            {
                string request = Console.ReadLine();
                if (request == null) { continue; }

                var response = dispatcher.Dispatch(JObject.Parse(request));
                System.Console.WriteLine(response.ToString(Formatting.None));
            }
        }

        private static object CreateProcessHandler(CreateProcessRequest request)
        {
            Debug.Assert(false);

            var createParams = request.@params;
            Process process = Process.Start(createParams.ToProcessStartInfo());
            return new CreateProcessResponse(
                request.id, 
                new CreateProcessResult
                {
                    Id = process.Id,
                });
        }
    }
}
