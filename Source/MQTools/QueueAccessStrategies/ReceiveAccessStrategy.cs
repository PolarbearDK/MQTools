using System;
using System.Messaging;

namespace MQTools.QueueAccessStrategies
{
    /// <summary>
    /// This strategy receives messages and returns it by appending it to end of queue. Message ID's change.
    /// Everything is done within a local message queue.
    /// </summary>
    public class ReceiveAccessStrategy : IQueueAccessStrategy
    {
        private readonly MessageQueue _queue;
        private MessageQueueTransaction _transaction;
        private bool _transactionHasPayload;
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1);

        public ReceiveAccessStrategy(MessageQueue queue)
        {
            _queue = queue;
            _transaction = new MessageQueueTransaction();
        }

        public void BeginBatch()
        {
            _transaction.Begin();
            _transactionHasPayload = false;
        }

        public void CommitBatch()
        {
            _transaction.Commit();
            _transactionHasPayload = false;
        }

        private void AbortBatch()
        {
            _transaction.Abort();
            _transactionHasPayload = false;
        }

        public Message GetNext()
        {
            try
            {
                return _queue.Receive(Timeout, _transaction);
            }
            catch (MessageQueueException ex)
            {
                if (ex.Message.Contains("Timeout"))
                {
                    return null;
                }
                
                if (ex.Message.Contains("The queue does not exist"))
                {
                    Console.Error.WriteLine("Unable to open the input queue: {0}", _queue.FormatName);
                    return null;
                }
                
                throw;
            }
        }

        public void Send(MessageQueue messageQueue, Message message)
        {
            messageQueue.Send(message, _transaction);
            _transactionHasPayload = true;
        }

        public void Delete(Message message)
        {
            // Delete is done by not sending message to end of queue.
        }

        public void Return(Message message)
        {
            Send(_queue, message);
        }

        public void UndoGetNext(Message message)
        {
            if (_transactionHasPayload)
            {
                // Other messages participate in this transaction. Send message to end of queue and commit
                Return(message);
                CommitBatch();
            }
            else
            {
                // This message is the only one in transaction. It is safe to rollback
                AbortBatch();
            }
        }

        public void Dispose()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }
        }
    }
}