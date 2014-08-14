using System;
using System.Messaging;

namespace MQTools.QueueAccessStrategies
{
    public interface IQueueAccessStrategy : IDisposable
    {
        void BeginBatch();
        void CommitBatch();
        Message GetNext();
        void Send(MessageQueue messageQueue, Message message);
        void Return(Message message);
        void Delete(Message message);
        void UndoGetNext(Message message);
    }
}