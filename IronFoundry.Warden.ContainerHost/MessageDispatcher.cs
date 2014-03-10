using IronFoundry.Warden.Shared.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronFoundry.Warden.ContainerHost
{
    public class MessageDispatcher
    {
        Dictionary<string, Func<JObject, object>> methods = new Dictionary<string, Func<JObject, object>>(StringComparer.OrdinalIgnoreCase);

        public JObject Dispatch(JObject request)
        {
            var method = (string)request["method"];

            Func<JObject, object> callback;
            if (methods.TryGetValue(method, out callback))
            {
                try
                {
                    var result = callback(request);

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

        public void RegisterMethod(string methodName, Func<JObject, object> callback)
        {
            methods.Add(methodName, callback);
        }

        public void RegisterMethod<T>(string methodName, Func<T, object> callback)
            where T: JsonRpcRequest
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
