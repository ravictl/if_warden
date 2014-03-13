using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using IronFoundry.Warden.Shared.Messaging;

namespace IronFoundry.Warden.Test.ContainerHost
{
    public class MessageTransportTest
    {
        MemoryStream outputStream = new MemoryStream();
        MemoryStream inputStream = new MemoryStream();

        StreamWriter inputStreamWriter = null;
        StreamReader inputStreamReader = null;

        StreamWriter outputStreamWriter = null;
        StreamReader outputStreamReader = null;

        MessageTransport transporter = null;

        public MessageTransportTest()
        {
            inputStreamWriter = new StreamWriter(inputStream) { AutoFlush = true };
            inputStreamReader = new StreamReader(inputStream);

            outputStreamWriter = new StreamWriter(outputStream) { AutoFlush = true };
            outputStreamReader = new StreamReader(outputStream);

            transporter = new MessageTransport(inputStreamReader, outputStreamWriter);
        }

        [Fact]
        public async void PublishSendsTextToWriter()
        {

            await transporter.PublishAsync(new JObject(new JProperty("foo", "bar")));

            outputStream.Position = 0;
            string output = await outputStreamReader.ReadLineAsync();

            Assert.Equal(@"{""foo"":""bar""}", output);
        }

        [Fact]
        public async void ReceivedRequestInvokesRequestCallbackForRequest()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""foo""}");
            inputStream.Position = 0;

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }

        [Fact]
        public async void ReceivedRequestDoesNotInvokeRequestCallbackForResponse()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""foo""}");
            inputStream.Position = 0;

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted); 
        }

        [Fact]
        public async void ReceivedResponseInvokesResponseCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}");
            inputStream.Position = 0;

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }

        [Fact]
        public async void ReceivedErrorResponseInvokesResponseCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""error"":{""code"":1,""message"":""foo-error""}}");
            inputStream.Position = 0;

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }

        [Fact]
        public async void ReceivedResponseDoesNotInvokesRequestCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}");
            inputStream.Position = 0;

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted); 
        }

        [Fact]
        public async void InvalidRequestDoesNotInvokeRequest()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"!@#$%&*()");
            inputStream.Position = 0;

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted); 
        }

        [Fact]
        public async void InvalidRequestDoesNotInvokeResponse()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse((request) => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"!@#$%&*()");
            inputStream.Position = 0;

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async void InvalidRequestNotifiesOfError()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeError(e => tcs.SetResult(0));

            await inputStreamWriter.WriteLineAsync(@"!@#$%&*()");
            inputStream.Position = 0;

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }

        [Fact]
        public async void StoppingWillHaltRequestPublication()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse((request) => tcs.SetResult(0));

            transporter.Stop();

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}");
            inputStream.Position = 0;

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async void StartWillRestartPublicationProcess()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse((request) => tcs.SetResult(0));

            transporter.Stop();
            transporter.Start();

            await inputStreamWriter.WriteLineAsync(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}");
            inputStream.Position = 0;

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }
    }
}
