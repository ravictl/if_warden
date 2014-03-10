using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace IronFoundry.Warden.ContainerHost
{
    public class MessageDispatcherTest
    {
        [Fact]
        public void MissingMethodReturnsError()
        {
            var dispatcher = new MessageDispatcher();
            var request = new JObject(
                new JProperty("id", "ID"),
                new JProperty("method", "NonExistentMethod"));

            var response = dispatcher.Dispatch(request);

            Assert.Equal(-32601, (int)response["error"]["code"]);
        }

        [Fact]
        public void DispatchesRequest()
        {
            var dispatcher = new MessageDispatcher();
            var called = false;
            dispatcher.RegisterMethod("testMethod", (requestMessage) =>
                {
                    called = true;
                    return 1;
                });

            var request = new JObject(
                new JProperty("id", "ID"),
                new JProperty("method", "testMethod"));

            var response = dispatcher.Dispatch(request);

            Assert.True(called);
        }

        [Fact]
        public void DipatchesRequestWithParameters()
        {
            var dispatcher = new MessageDispatcher();
            JObject @params = null;
            dispatcher.RegisterMethod("testMethod", (r) =>
            {
                @params = (JObject)r["params"];
                return 1;
            });

            var request = new JObject(
                new JProperty("id", "ID"),
                new JProperty("method", "testMethod"),
                new JProperty("params", new JObject(
                    new JProperty("a", 1),
                    new JProperty("b", "string")
                ))
            );

            var response = dispatcher.Dispatch(request);

            Assert.Equal(1, (int)@params["a"]);
            Assert.Equal("string", (string)@params["b"]);
        }

        [Fact]
        public void ReturnsResultFromCallback()
        {
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterMethod("testMethod", (@params) =>
            {
                return "result";
            });

            var request = new JObject(
                new JProperty("id", "ID"),
                new JProperty("method", "testMethod"));

            var response = dispatcher.Dispatch(request);

            Assert.Equal("result", (string)response["result"]);
        }

        [Fact]
        public void ThrowingCallbackReturnsErrorResponse()
        {
            var dispatcher = new MessageDispatcher();
            dispatcher.RegisterMethod("testMethod", (@params) =>
            {
                throw new Exception("ERROR");
            });

            var request = new JObject(
                new JProperty("id", "ID"),
                new JProperty("method", "testMethod"));

            var response = dispatcher.Dispatch(request);

            var error = (JObject)response["error"];
            Assert.Equal(-32603, (int)error["code"]);
            Assert.Equal("ERROR", (string)error["message"]);
            Assert.Contains(System.Reflection.MethodInfo.GetCurrentMethod().Name, (string)error["data"]);
        }

        [Fact]
        public void DispatchesStronglyTypedRequest()
        {
            var dispatcher = new MessageDispatcher();
            bool called = false;
            dispatcher.RegisterMethod<JsonRpcRequest>("testMethod", (r) =>
            {
                called = true;
                return 1;
            });

            var request = new JObject(
                new JProperty("id", "ID"),
                new JProperty("method", "testMethod"));
            
            var response = dispatcher.Dispatch(request);

            Assert.True(called);
        }

    }
}
