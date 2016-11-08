using DQueue.Helpers;
using DQueue.Infrastructure;
using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DQueue.QueueProviders
{
    public class AspNetProvider : IQueueProvider
    {
        #region static
        static readonly Dictionary<string, List<string>> _queues;
        static readonly Dictionary<string, HashSet<string>> _hashs;

        static AspNetProvider()
        {
            _queues = new Dictionary<string, List<string>>();
            _hashs = new Dictionary<string, HashSet<string>>();
        }

        private static List<string> GetQueue(string key)
        {
            if (!_queues.ContainsKey(key))
            {
                lock (typeof(AspNetProvider))
                {
                    if (!_queues.ContainsKey(key))
                    {
                        _queues.Add(key, new List<string>());
                    }
                }
            }

            return _queues[key];
        }

        private static HashSet<string> GetHashSet(string key)
        {
            if (!_hashs.ContainsKey(key))
            {
                lock (typeof(AspNetProvider))
                {
                    if (!_hashs.ContainsKey(key))
                    {
                        _hashs.Add(key, new HashSet<string>());
                    }
                }
            }

            return _hashs[key];
        }
        #endregion

        public bool IgnoreHash { get; set; }

        public bool ExistsMessage(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return false;
            }

            var json = message.Serialize();
            var hash = json.GetMD5();

            var hashSet = GetHashSet(queueName);
            return hashSet.Contains(hash);
        }

        public void Enqueue(string queueName, object message)
        {
            if (string.IsNullOrWhiteSpace(queueName) || message == null)
            {
                return;
            }

            var json = message.Serialize();
            var queue = GetQueue(queueName);

            string hash = null;
            HashSet<string> hashSet = null;
            if (!IgnoreHash)
            {
                hash = json.GetMD5();
                hashSet = GetHashSet(queueName);
                if (hashSet.Contains(hash))
                {
                    return;
                }
            }

            var hostId = DQueueSettings.Get().HostId;
            var dequeueLocker = ReceptionAssistant.GetLocker(queueName + string.Format(Constants.DequeueLockerFlag, hostId));

            lock (dequeueLocker)
            {
                queue.Add(json.AddEnqueueTime());

                if (!IgnoreHash)
                {
                    hashSet.Add(hash);
                }

                Monitor.Pulse(dequeueLocker);
            }
        }

        public void Dequeue<TMessage>(ReceptionAssistant<TMessage> assistant, Action<ReceptionContext<TMessage>> handler)
        {
            if (assistant == null || string.IsNullOrWhiteSpace(assistant.QueueName) || handler == null)
            {
                return;
            }

            RequeueProcessingMessages(assistant);

            assistant.Disposing += (s, e) =>
            {
                lock (assistant.DequeueLocker)
                {
                    Monitor.PulseAll(assistant.DequeueLocker);
                }

                RequeueProcessingMessages(assistant);
            };

            while (!assistant.IsTerminated())
            {
                var message = default(TMessage);
                var rawMessage = default(string);

                lock (assistant.DequeueLocker)
                {
                    var queue = GetQueue(assistant.QueueName);
                    var queueProcessing = GetQueue(assistant.ProcessingQueueName);

                    if (queue.Count == 0)
                    {
                        Monitor.Wait(assistant.DequeueLocker);
                    }

                    try
                    {
                        rawMessage = queue[0];
                        queue.RemoveAt(0);
                        queueProcessing.Add(rawMessage);
                        message = rawMessage.Deserialize<TMessage>();
                    }
                    catch (Exception ex)
                    {
                        LogFactory.GetLogger().Error(string.Format("[AspNetProvider] Get Message Error! Raw Message: \"{0}\".", rawMessage), ex);
                        RemoveProcessingMessage(assistant, queueProcessing, rawMessage);
                    }
                }

                if (message != null)
                {
                    handler(new ReceptionContext<TMessage>(message, rawMessage, assistant, FeedbackHandler));
                }
            }
        }

        private void RemoveProcessingMessage<TMessage>(ReceptionAssistant<TMessage> assistant, List<string> queueProcessing, string rawMessage)
        {
            if (rawMessage == null)
            {
                return;
            }

            try
            {
                var hashSet = GetHashSet(assistant.QueueName);

                queueProcessing.Remove(rawMessage);
                hashSet.Remove(rawMessage.RemoveEnqueueTime().GetMD5());
            }
            catch { }
        }

        private void FeedbackHandler<TMessage>(ReceptionContext<TMessage> context, DispatchStatus status)
        {
            var assistant = context.Assistant;
            var rawMessage = (string)context.RawMessage;
            var queue = GetQueue(assistant.QueueName);
            var queueProcessing = GetQueue(assistant.ProcessingQueueName);

            if (status == DispatchStatus.Complete)
            {
                RemoveProcessingMessage(context.Assistant, queueProcessing, rawMessage);
            }
            else if (status == DispatchStatus.Timeout)
            {
                queueProcessing.Remove(rawMessage);
                queue.Add(rawMessage.RemoveEnqueueTime().AddEnqueueTime());
            }
        }

        private void RequeueProcessingMessages<TMessage>(ReceptionAssistant<TMessage> assistant)
        {
            var queue = GetQueue(assistant.QueueName);

            var queueProcessing = GetQueue(assistant.ProcessingQueueName);

            foreach (var item in queueProcessing)
            {
                queue.Insert(0, item);
            }

            queueProcessing.Clear();
        }
    }
}
