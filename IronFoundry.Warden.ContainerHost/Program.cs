using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IronFoundry.Warden.Shared.Messaging;

namespace IronFoundry.Warden.ContainerHost
{
    class ProcessContext
    {
        public ProcessContext()
        {
            StandardError = new StringBuilder();
            StandardOutputTail = new Queue<string>();
        }

        public bool HasExited { get; set; }
        public int ExitCode { get; set; }
        public StringBuilder StandardError { get; set; }
        public Queue<string> StandardOutputTail { get; set; }

        public void HandleErrorData(object sender, DataReceivedEventArgs e)
        {
            StandardError.AppendLine(e.Data);
        }

        public void HandleOutputData(object sender, DataReceivedEventArgs e)
        {
            while (StandardOutputTail.Count > 100)
                StandardOutputTail.Dequeue();

            StandardOutputTail.Enqueue(e.Data);
        }

        public void HandleProcessExit(object sender, EventArgs e)
        {
            var process = (Process)sender;

            HasExited = true;
            ExitCode = process.ExitCode;
        }
    }

    class Program
    {
        static ManualResetEvent exitEvent = new ManualResetEvent(false);
        static ConcurrentDictionary<int, ProcessContext> processContexts = new ConcurrentDictionary<int, ProcessContext>();

        static void Main(string[] args)
        {
            var input = Console.In;
            var output = Console.Out;
            using (var transport = new MessageTransport(input, output))
            {
                var dispatcher = new MessageDispatcher();
                dispatcher.RegisterMethod<CreateProcessRequest>("CreateProcess", CreateProcessHandler);
                dispatcher.RegisterMethod<GetProcessExitInfoRequest>("GetProcessExitInfo", GetProcessExitInfoHandler);

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
            
            var startInfo = createParams.ToProcessStartInfo();
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            var processContext = new ProcessContext();

            Process process = Process.Start(startInfo);

            process.ErrorDataReceived += processContext.HandleErrorData;
            process.OutputDataReceived += processContext.HandleOutputData;
            process.Exited += processContext.HandleProcessExit;

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.EnableRaisingEvents = true;
            
            processContexts[process.Id] = processContext;

            return Task.FromResult<object>(
                new CreateProcessResponse(
                    request.id,
                    new CreateProcessResult
                    {
                        Id = process.Id,
                    }));
        }

        private static Task<object> GetProcessExitInfoHandler(GetProcessExitInfoRequest request)
        {
            //Debug.Assert(false);

            ProcessContext processContext;
            if (processContexts.TryGetValue(request.@params.Id, out processContext))
            {
                return Task.FromResult<object>(
                    new GetProcessExitInfoResponse(
                        request.id,
                        new GetProcessExitInfoResult
                        {
                            ExitCode = processContext.ExitCode,
                            HasExited = processContext.HasExited,
                            StandardError = processContext.StandardError.ToString(),
                            StandardOutputTail = String.Join("\n", processContext.StandardOutputTail),
                        }));
            }
            else
            {
                throw new Exception("The process doesn't exist.");
            }
        }
    }
}
