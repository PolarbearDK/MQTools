using System.Messaging;
using NServiceBus;
using NServiceBus.Transports.Msmq;

namespace MQTools
{
    public static class QueueFactory
    {
        public static MessageQueue GetQueue(string computerName, string queue)
        {
            return GetQueue(computerName, "Private$", queue);
        }

        public static MessageQueue GetQueue(string computerName, string queueType, string queue)
        {
            string queuePath;
            if (string.IsNullOrEmpty(computerName))
            {
                queuePath = MsmqUtilities.GetFullPath(Address.Parse(queue));
            }
            else
            {
                queuePath = string.Format("{0}\\{1}\\{2}",
                                          string.IsNullOrEmpty(computerName) ? "." : computerName,
                                          queueType,
                                          queue);
            }
            return new MessageQueue(queuePath);
        }
    }
}
