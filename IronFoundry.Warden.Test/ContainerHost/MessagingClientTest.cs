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
    public class MessagingClient
    {
        private Action<string> transportHandler;
        private Dictionary<string, TaskCompletionSource<JsonRpcResponse>> awaitingResponse = new Dictionary<string, TaskCompletionSource<JsonRpcResponse>>();

        public MessagingClient(Action<string> transportHandler)
        {
            this.transportHandler = transportHandler;
        }

        public Task<JsonRpcResponse> SendMessageAsync(JsonRpcRequest r)
        {
            var tcs = new TaskCompletionSource<JsonRpcResponse>();
            awaitingResponse.Add(r.id, tcs);

            transportHandler(JsonConvert.SerializeObject(r, Formatting.None));

            return tcs.Task;
        }

        public void PublishResponse(JObject response)
        {
            string id = response["id"].ToString();
            TaskCompletionSource<JsonRpcResponse> tcs;
            if (awaitingResponse.TryGetValue(id, out tcs))
            {
                if (response["error"] != null)
                {
                    var errorResponse = new JsonRpcErrorResponse(id);
                    errorResponse.error.Code = (int)response["error"]["code"];
                    errorResponse.error.Message = response["error"]["message"].ToString();
                    errorResponse.error.Data = response["error"]["data"] == null ? null : response["error"]["data"].ToString();

                    tcs.SetResult(errorResponse);
                }
                else
                {
                    var rpcResponse = new JsonRpcResponse(id);
                    tcs.SetResult(rpcResponse);
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

        //[Fact]
        //public async void StronglyTypedRequestsReturnsStronglyTypedResponse()
        //{
        //    MessagingClient client = null;


        //}

        // Strongly typed handling
        // Uncorrelated Response throws?
        // Request timesout
        // Disposes completes awaiting tasks
        // 
    }
}
