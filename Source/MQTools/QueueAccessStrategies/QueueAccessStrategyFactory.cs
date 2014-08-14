using System;
using System.Messaging;

namespace MQTools.QueueAccessStrategies
{
    public static class QueueAccessStrategyFactory
    {
        public static IQueueAccessStrategy Create(QueueAccessStrategy strategy, MessageQueue queue)
        {
            switch (strategy)
            {
                case QueueAccessStrategy.Cursor:
                    return new CursorAccessStrategy(queue);
                case QueueAccessStrategy.Receive:
                    return new ReceiveAccessStrategy(queue);
                default:
                    throw new ArgumentOutOfRangeException("strategy");
            }
        }
    }
}