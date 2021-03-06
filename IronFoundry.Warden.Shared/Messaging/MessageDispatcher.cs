﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace IronFoundry.Warden.Shared.Messaging
{
    public class MessageDispatcher
    {
        Dictionary<string, Func<JObject, Task<object>>> methods = new Dictionary<string, Func<JObject, Task<object>>>(StringComparer.OrdinalIgnoreCase);

        public async Task<JObject> DispatchAsync(JObject request)
        {
            var method = (string)request["method"];

            Func<JObject, Task<object>> callback;
            if (methods.TryGetValue(method, out callback))
            {
                try
                {
                    var result = await callback(request);

                    if (result is JsonRpcResponse)
                    {
                        return JObject.FromObject(result);
                    }

                    return SuccessResponse(request["id"], result);
                }
                catch (Exception ex)
                {
                    return InternalError(request, ex);
                }
            }
            else
            {
                return MethodNotFoundError(request, method);
            }
        }

        static JObject ErrorResponse(JToken id, int code, string message = null, JToken data = null)
        {
            return new JObject(
                new JProperty("jsonrpc", "2.0"),
                new JProperty("id", id),
                new JProperty("error",
                    new JObject(
                        new JProperty("code", code),
                        new JProperty("message", message ?? "Unknown error"),
                        new JProperty("data", data)
                    )
                )
            );
        }

        static JObject InternalError(JObject request, Exception exception)
        {
            return ErrorResponse(request["id"], -32603, exception.Message, JToken.FromObject(exception.StackTrace));
        }

        static JObject MethodNotFoundError(JObject request, string methodName)
        {
            return ErrorResponse(request["id"], -32601, String.Format("The method '{0}' does not exist.", methodName));
        }

        public void RegisterMethod(string methodName, Func<JObject, Task<object>> callback)
        {
            methods.Add(methodName, callback);
        }

        public void RegisterMethod<T>(string methodName, Func<T, Task<object>> callback)
            where T : JsonRpcRequest
        {
            methods.Add(methodName, (r) =>
            {
                return callback((T)r.ToObject<T>());
            });
        }

        public JObject SuccessResponse(JToken id, object result)
        {
            return new JObject(
                new JProperty("jsonrpc", "2.0"),
                new JProperty("id", id),
                new JProperty("result", JToken.FromObject(result))
            );
        }
    }
}
