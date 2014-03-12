using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IronFoundry.Warden.ContainerHost
{
    class Program
    {
        static ManualResetEvent exitEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            var task = MainAsync(args);
            task.GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            var input = Console.In;
            var output = Console.Out;
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterMethod<CreateProcessRequest>("CreateProcess", CreateProcessHandler);

            while (!exitEvent.WaitOne(0))
            {
                string request = await input.ReadLineAsync();
                if (String.IsNullOrWhiteSpace(request))
                    continue;

                var response = await dispatcher.DispatchAsync(JObject.Parse(request));

                await output.WriteLineAsync(response.ToString(Formatting.None));
            }
        }

        private static Task<object> CreateProcessHandler(CreateProcessRequest request)
        {
            //Debug.Assert(false);

            var createParams = request.@params;
            Process process = Process.Start(createParams.ToProcessStartInfo());
            return Task.FromResult<object>(
                new CreateProcessResponse(
                    request.id,
                    new CreateProcessResult
                    {
                        Id = process.Id,
                    }));
        }
    }
}
