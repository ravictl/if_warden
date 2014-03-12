using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IronFoundry.Warden.Test.ContainerHost
{
    class MessageTransport
    {
        private TextReader reader;
        private TextWriter writer;
        private Task<string> pendingReadLine;
        private List<Action<JObject>> requestCallbacks = new List<Action<JObject>>();

        public MessageTransport(TextReader reader, TextWriter writer)
        {
            this.reader = reader;
            this.writer = writer;

            this.pendingReadLine = reader.ReadLineAsync();
            this.pendingReadLine.ContinueWith(HandleReadLine);
        }

        void HandleReadLine(Task<string> task)
        {
            if (!String.IsNullOrWhiteSpace(task.Result))
            {
                var message = JObject.Parse(task.Result);

                lock (requestCallbacks)
                {
                    foreach (var callback in requestCallbacks)
                    {
                        callback(message);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("Queuing read line.");
            this.pendingReadLine = reader.ReadLineAsync();
            this.pendingReadLine.ContinueWith(HandleReadLine);
        }

        public Task PublishAsync(JObject message)
        {
            string text = message.ToString(Formatting.None);
            return writer.WriteLineAsync(text);
        }

        public void SubscribeRequest(Action<JObject> callback)
        {
            lock (requestCallbacks)
            {
                requestCallbacks.Add(callback);
            }
        }

        public void SubscribeResponse(Action<JObject> callback)
        {
        }
    }

    public class MessageTransportTest
    {
        [Fact]
        public async void PublishSendsTextToWriter()
        {
            var outputStream = new MemoryStream();
            var writer = new StreamWriter(outputStream) { AutoFlush = true };
            var reader = new StreamReader(outputStream);

            var transporter = new MessageTransport(null, writer);

            await transporter.PublishAsync(new JObject(new JProperty("foo", "bar")));

            outputStream.Position = 0;
            string output = await reader.ReadLineAsync();

            Assert.Equal(@"{""foo"":""bar""}", output);
        }

        [Fact]
        public async void ReceivedRequestInvokesRequestCallbackForRequest()
        {
            var inputStream = new MemoryStream();
            var writer = new StreamWriter(inputStream) { AutoFlush = true };
            var reader = new StreamReader(inputStream);

            var transporter = new MessageTransport(reader, null);

            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest((request) => tcs.SetResult(0));

            writer.WriteLine(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""foo""}");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }

        //[Fact]
        //public async void ReceivedRequestDoesNotInvokeRequestCallbackForResponse()
        //{
        //}
    }
}
