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
            var input = Console.In;
            var output = Console.Out;
            using (var transport = new MessageTransport(input, output))
            {
                var dispatcher = new MessageDispatcher();
                dispatcher.RegisterMethod<CreateProcessRequest>("CreateProcess", CreateProcessHandler);

                transport.SubscribeRequest(
                    async (request) =>
                    {
                        var response = await dispatcher.DispatchAsync(request);
                        await transport.PublishAsync(response);
                    });

                exitEvent.WaitOne();
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
