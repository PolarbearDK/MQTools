using System.Messaging;
using NServiceBus;
using NServiceBus.Transports.Msmq;

namespace MQTools
{
    public static class QueueFactory
    {
        public static MessageQueue GetInputQueue(string server, string queue)
        {
            MessageQueue messageQueue = GetQueue(server, "Private$", queue);
            messageQueue.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
            var messagePropertyFilter = new MessagePropertyFilter();
            messagePropertyFilter.SetAll();
            messageQueue.MessageReadPropertyFilter = messagePropertyFilter;

            return messageQueue;
        }

        public static MessageQueue GetOutputQueue(string server, string queue)
        {
            return GetQueue(server, "Private$", queue);
        }

        private static MessageQueue GetQueue(string server, string queueType, string queue)
        {
            string queuePath;
            if (string.IsNullOrEmpty(server))
            {
                queuePath = MsmqUtilities.GetFullPath(Address.Parse(queue));
            }
            else
            {
                queuePath = string.Format("{0}\\{1}\\{2}",
                                          string.IsNullOrEmpty(server) ? "." : server,
                                          queueType,
                                          queue);
            }
            return new MessageQueue(queuePath);
        }

        public static string GetQueueName(string server, string queue)
        {
            return string.IsNullOrEmpty(server) 
                ? queue 
                : queue + "@" + server;
        }
    }
}
