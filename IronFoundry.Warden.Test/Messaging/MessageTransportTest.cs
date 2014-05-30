﻿using System;
using System.IO;
using System.Threading.Tasks;
using IronFoundry.Warden.Shared.Messaging;
using IronFoundry.Warden.Test.TestSupport;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Xunit;

namespace IronFoundry.Warden.Test.ContainerHost
{
    public class MessageTransportTest : IDisposable
    {
        InputSource inputSource = new InputSource();
        MessageTransport transporter = null;

        public MessageTransportTest()
        {
            var outputWriter = Substitute.For<TextWriter>();
            transporter = new MessageTransport(inputSource, outputWriter);
        }

        public void Dispose()
        {
            inputSource.Dispose();
            transporter.Stop();
        }

        [Fact]
        public async void PublishRequestSendsWrappedRequestToTextToWriter()
        {
            var outputSink = new TaskCompletionSource<string>();
            var outputWriter = Substitute.For<TextWriter>();
            outputWriter.When(x => x.WriteLine(Arg.Any<string>())).Do(callInfo =>
                outputSink.SetResult(callInfo.Arg<string>()));

            using (var transporter = new MessageTransport(inputSource, outputWriter))
            {
                await transporter.PublishRequestAsync(new JObject(new JProperty("foo", "bar")));

                string output = await outputSink.Task;

                Assert.Equal(@"{""content_type"":""Request"",""body"":{""foo"":""bar""}}", output);
            }
        }

        [Fact]
        public async void PublishResponseSendsWrappedResponseToTextWriter()
        {
            var outputSink = new TaskCompletionSource<string>();
            var outputWriter = Substitute.For<TextWriter>();
            outputWriter.When(x => x.WriteLine(Arg.Any<string>())).Do(callInfo =>
                outputSink.SetResult(callInfo.Arg<string>()));

            using (var transporter = new MessageTransport(inputSource, outputWriter))
            {
                await transporter.PublishResponseAsync(new JObject(new JProperty("foo", "bar")));

                string output = await outputSink.Task;

                Assert.Equal(@"{""content_type"":""Response"",""body"":{""foo"":""bar""}}", output);
            }
        }


        [Fact]
        public async void PublishedEventSendsWrappedEventToTextWriter()
        {
            var outputSink = new TaskCompletionSource<string>();
            var outputWriter = Substitute.For<TextWriter>();
            outputWriter.When(x => x.WriteLine(Arg.Any<string>())).Do(callInfo =>
                outputSink.SetResult(callInfo.Arg<string>()));

            using (var transporter = new MessageTransport(inputSource, outputWriter))
            {
                await transporter.PublishEventAsync(new JObject(new JProperty("foo", "bar")));

                string output = await outputSink.Task;

                Assert.Equal(@"{""content_type"":""Event"",""body"":{""foo"":""bar""}}", output);
            }
        }

        [Fact]
        public async void ReceivedRequestInvokesRequestCallbackForRequest()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Request"", ""body"":{""jsonrpc"":""2.0"",""id"":1,""method"":""foo""}}");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }

        [Fact]
        public async void ReceivedRequestDoesNotInvokeRequestCallbackForResponse()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Request"",""body"":{""jsonrpc"":""2.0"",""id"":1,""method"":""foo""}}");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted); 
        }

        [Fact]
        public async void ReceivedResponseInvokesResponseCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Response"",""body"":{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}}");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(2000)));
        }

        [Fact]
        public async void ReceivedErrorResponseInvokesResponseCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Response"",""body"":{""jsonrpc"":""2.0"",""id"":1,""error"":{""code"":1,""message"":""foo-error""}}}");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(2000)));
        }

        [Fact]
        public async void ReceviedEventInvokesEventCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeEvent(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Event"",""body"":{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}}");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
        }

        [Fact]
        public async void ReceivedResponseDoesNotInvokesRequestCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Response"",""body"":{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}}");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted); 
        }

        [Fact]
        public async void ReceivedResponseDoesNotInvokesEventCallback()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeEvent(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"{""content_type"":""Response"",""body"":{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}}");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async void InvalidRequestDoesNotInvokeRequest()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeRequest(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"!@#$%&*()");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted); 
        }

        [Fact]
        public async void InvalidRequestDoesNotInvokeResponse()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"!@#$%&*()");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async void InvalidInputDoesNotInvokeError()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeEvent(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(@"!@#$%&*()");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async void InvalidRequestNotifiesOfError()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeError(e => tcs.SetResult(0));

            inputSource.AddLine(@"!@#$%&*()");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(3000)));
        }

        [Fact]
        public async void StoppingWillHaltRequestPublication()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(null);
            transporter.Stop();

            inputSource.AddLine(@"{""content_type"":""Response"",""body"":{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}}");

            Assert.NotSame(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(150)));
            Assert.False(tcs.Task.IsCompleted);
        }

        [Fact]
        public async void StartWillRestartPublicationProcess()
        {
            var tcs = new TaskCompletionSource<int>();
            transporter.SubscribeResponse(r => { tcs.SetResult(0); return Task.FromResult(0); });

            inputSource.AddLine(null);
            transporter.Stop();
            transporter.Start();

            inputSource.AddLine(@"{""content_type"":""Response"",""body"":{""jsonrpc"":""2.0"",""id"":1,""result"":""foo-result""}}");

            Assert.Same(tcs.Task, await Task.WhenAny(tcs.Task, Task.Delay(1000)));
        }
    }
}
