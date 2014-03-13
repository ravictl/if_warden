using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IronFoundry.Warden.Shared.Messaging
{
    public class MessageTransport
    {
        private TextReader reader;
        private TextWriter writer;
        private List<Action<JObject>> requestCallbacks = new List<Action<JObject>>();
        private List<Action<JObject>> responseCallbacks = new List<Action<JObject>>();
        private List<Action<Exception>> errorSubscribers = new List<Action<Exception>>();
        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private CancellationToken token;
        private Task readTask;

        public MessageTransport(TextReader reader, TextWriter writer)
        {
            this.reader = reader;
            this.writer = writer;

            Start();
        }

        public Task HandleLine(Task<string> readTask)
        {
            if (!string.IsNullOrEmpty(readTask.Result))
                InvokeCallback(readTask.Result);

            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();

            return ReadAsync();
        }

        private void InvokeCallback(string stringMessage)
        {
            JObject message = null;
            try
            {
                message = JObject.Parse(stringMessage);
            }
            catch (Exception e)
            {
                InvokeErrors(e);
            }

            if (IsResponseMessage(message))
                InvokeResponseCallback(message);
            else
                InvokeRequestCallback(message);
        }

        private bool IsResponseMessage(JObject message)
        {
            return (message["result"] != null || message["error"] != null);
        }

        private void InvokeRequestCallback(JObject message)
        {
            lock (requestCallbacks)
            {
                foreach (var callback in requestCallbacks)
                {
                    callback(message);
                }
            }
        }

        private void InvokeResponseCallback(JObject message)
        {
            lock (responseCallbacks)
            {
                foreach (var callback in responseCallbacks)
                {
                    callback(message);
                }
            }
        }

        private void InvokeErrors(Exception e)
        {
            lock (errorSubscribers)
            {
                foreach (var callback in errorSubscribers)
                {
                    try
                    {
                        callback(e);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public Task PublishAsync(JObject message)
        {
            string text = message.ToString(Formatting.None);
            return writer.WriteLineAsync(text);
        }

        private Task ReadAsync()
        {
            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            return reader.ReadLineAsync()
                .ContinueWith<Task>(HandleLine);
        }

        public void Start()
        {
            readTask = ReadAsync();
        }
        
        public void Stop()
        {
            tokenSource.Cancel();
            readTask = null;
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
            lock (responseCallbacks)
            {
                responseCallbacks.Add(callback);
            }
        }

        public void SubscribeError(Action<Exception> callback)
        {
            lock (errorSubscribers)
            {
                errorSubscribers.Add(callback);
            }
        }
    }
}
