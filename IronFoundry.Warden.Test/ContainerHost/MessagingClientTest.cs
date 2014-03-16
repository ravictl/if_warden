using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;


namespace IronFoundry.Warden.Test.ContainerHost
{
    [Serializable]
    public class MessagingException : Exception
    {
        public MessagingException() { }
        public MessagingException(string message) : base(message) { }
        public MessagingException(string message, Exception inner) : base(message, inner) { }
        protected MessagingException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }

        public JsonRpcErrorResponse ErrorResponse { get; set; }
    }

    public class MessagingClient
    {
        private Action<string> transportHandler;
        private Dictionary<string, ResponsePublisher> awaitingResponse =
            new Dictionary<string, ResponsePublisher>();

        public MessagingClient(Action<string> transportHandler)
        {
            this.transportHandler = transportHandler;
        }

        public Task<JsonRpcResponse> SendMessageAsync(JsonRpcRequest r)
        {
            var publisher = new DefaultResponsePublisher();
            awaitingResponse.Add(r.id, publisher);
            transportHandler(JsonConvert.SerializeObject(r, Formatting.None));
            return publisher.Task;
        }

        public Task<TResult> SendMessageAsync<T, TResult>(T request)
            where T: JsonRpcRequest
            where TResult: JsonRpcResponse
        {
            var publisher = new StronglyTypedResponsePublisher<TResult>();
            awaitingResponse.Add(request.id, publisher);
            transportHandler(JsonConvert.SerializeObject(request, Formatting.None));
            return publisher.Task;
        }

        public void PublishResponse(JObject response)
        {
            string id = response["id"].ToString();
            ResponsePublisher publisher;
            if (awaitingResponse.TryGetValue(id, out publisher))
            {
                publisher.Publish(response);
            }
            else
            {
                throw new MessagingException("No one waiting for response " + id);
            }
        }

        private abstract class ResponsePublisher
        {
            abstract public void Publish(JObject response);

            protected bool IsErrorResponse(JObject response)
            {
                return (response["error"] != null);
            }

            protected JsonRpcErrorResponse BuildErrorResponse(JObject error)
            {
                var errorResponse = new JsonRpcErrorResponse(error["id"].ToString());
                errorResponse.error.Code = (int)error["error"]["code"];
                errorResponse.error.Message = error["error"]["message"].ToString();
                errorResponse.error.Data = error["error"]["data"] == null ? null : error["error"]["data"].ToString();

                return errorResponse;
            }
        }

        private class DefaultResponsePublisher : ResponsePublisher
        {
            TaskCompletionSource<JsonRpcResponse> tcs = new TaskCompletionSource<JsonRpcResponse>();

            public DefaultResponsePublisher()
            {
            }

            public override void Publish(JObject arg)
            {
                if (IsErrorResponse(arg))
                {
                    tcs.SetResult(BuildErrorResponse(arg));
                }
                else
                {
                    var rpcResponse = new JsonRpcResponse(arg["id"].ToString());
                    tcs.SetResult(rpcResponse);
                }
            }

         public Task<JsonRpcResponse> Task
            {
                get
                {
                    return tcs.Task;
                }
            }
        }

        private class StronglyTypedResponsePublisher<TResponse> : ResponsePublisher
            where TResponse : JsonRpcResponse
        {
            private TaskCompletionSource<TResponse> tcs = new TaskCompletionSource<TResponse>();

            public StronglyTypedResponsePublisher()
            {
            }

            override public void Publish(JObject response)
            {
                if (IsErrorResponse(response))
                {
                    var error = BuildErrorResponse(response);
                    tcs.SetException(new MessagingException() { ErrorResponse = error });
                }
                else
                {
                    tcs.SetResult(response.ToObject<TResponse>());
                }
            }

            public Task<TResponse> Task
            {
                get
                {
                    return tcs.Task;
                }
            }
        }
    }

    public class MessagingClientTest
    {
        [Fact]
        public void InvokesSenderToSendMessage()
        {
            bool invoked = false;
            MessagingClient client = null;
            JsonRpcRequest r = new JsonRpcRequest("TestMethod");

            client = new MessagingClient(
            (m =>
                invoked = true
            ));

            client.SendMessageAsync(r);

            Assert.True(invoked);
        }

        [Fact]
        public async void CorrelatesResponseWithRequest()
        {
            MessagingClient client = null;
            JsonRpcRequest r = new JsonRpcRequest("TestMethod");

            client = new MessagingClient(m =>
            {
                client.PublishResponse(new JObject(
                    new JProperty("jsonrpc", "2.0"),
                    new JProperty("id", r.id),
                    new JProperty("result", "0")
                    ));
            });

            var response = await client.SendMessageAsync(r);

            Assert.Equal(r.id, response.id);

        }

        [Fact]
        public async void CorrelatesErrorResponseWithRequest()
        {
            MessagingClient client = null;
            JsonRpcRequest r = new JsonRpcRequest("TestMethod");

            client = new MessagingClient(m => {
                client.PublishResponse(
                    new JObject(
                        new JProperty("jsonrpc", "2.0"),
                        new JProperty("id", r.id),
                        new JProperty("error",
                            new JObject(
                                new JProperty("code", 1),
                                new JProperty("message", "Error Message")
                                )
                            )
                        )
                    );
            });

            var response = await client.SendMessageAsync(r);

            Assert.IsType<JsonRpcErrorResponse>(response);
        }

        [Fact]
        public async void ErrorResonseIncludesErrorData()
        {
            MessagingClient client = null;
            JsonRpcRequest r = new JsonRpcRequest("TestMethod");

            client = new MessagingClient(m =>
            {
                client.PublishResponse(
                    new JObject(
                        new JProperty("jsonrpc", "2.0"),
                        new JProperty("id", r.id),
                        new JProperty("error",
                            new JObject(
                                new JProperty("code", 1),
                                new JProperty("message", "Error Message"),
                                new JProperty("data", "Error Data")
                                )
                            )
                        )
                    );
            });

            var response = (JsonRpcErrorResponse) await client.SendMessageAsync(r);

            Assert.Equal(1, response.error.Code);
            Assert.Equal("Error Message", response.error.Message);
            Assert.Equal("Error Data", response.error.Data);
        }


        [Fact]
        public async void StronglyTypedRequestsReturnsStronglyTypedResponse()
        {
            MessagingClient client = null;
            var r = new CustomRequest();

            client = new MessagingClient(m =>
            {
                client.PublishResponse(new JObject(
                    new JProperty("jsonrpc", "2.0"),
                    new JProperty("id", r.id),
                    new JProperty("result", "ResultData")
                    ));
            });

            CustomResponse response = await client.SendMessageAsync<CustomRequest,CustomResponse>(r);
            Assert.NotNull(response);
        }

        [Fact]
        public async void StronglyTypedResponseContainsProperResults()
        {
            MessagingClient client = null;
            var r = new CustomRequest();

            client = new MessagingClient(m =>
            {
                client.PublishResponse(new JObject(
                    new JProperty("jsonrpc", "2.0"),
                    new JProperty("id", r.id),
                    new JProperty("result", "ResultData")
                    ));
            });

            CustomResponse response = await client.SendMessageAsync<CustomRequest, CustomResponse>(r);
            Assert.Equal("ResultData", response.result);
        }

        [Fact]
        public void StronglyTypedRequestErrorsThrowsProperExceptionType()
        {
            MessagingClient client = null;
            var r = new CustomRequest();

            client = new MessagingClient(m =>
            {
                client.PublishResponse(
                    new JObject(
                        new JProperty("jsonrpc", "2.0"),
                        new JProperty("id", r.id),
                        new JProperty("error",
                            new JObject(
                                new JProperty("code", 1),
                                new JProperty("message", "Error Message"),
                                new JProperty("data", "Error Data")
                                )
                            )
                        )
                    );
            });

            Exception recordedExeption = Record.Exception( () => {
                var responseTask = client.SendMessageAsync<CustomRequest, CustomResponse>(r);
                var result = responseTask.Result;
            });

            Assert.IsType<MessagingException>(((AggregateException)recordedExeption).InnerExceptions[0]);
        }

        [Fact]
        public void ThrowsWhenReceivingAnUncorrelatableResponse()
        {
            MessagingClient client = new MessagingClient(s => { }) ;
            var r = new JsonRpcRequest("TestMethod");

            var exception = Record.Exception(() =>
            {
                client.PublishResponse(
                new JObject(
                   new JProperty("jsonrpc", "2.0"),
                   new JProperty("id", r.id + "_notit"),
                   new JProperty("result", "0")
                   ));
            });

            Assert.IsType<MessagingException>(exception);
        }

        // Uncorrelated Response throws?
        // Request timesout
        // Disposes completes awaiting tasks
        // 

        class CustomRequest : JsonRpcRequest
        {
            public CustomRequest() : base("CustomRequestMethod")
            {
            }
        }

        class CustomResponse : JsonRpcResponse<string>
        {
        }

    }
}
