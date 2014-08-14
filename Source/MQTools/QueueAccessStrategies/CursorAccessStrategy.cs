using System;
using System.Messaging;

namespace MQTools.QueueAccessStrategies
{
    /// <summary>
    /// This strategy uses a cursor without transaction to read messages, and a local transaction to send/delete messages
    /// </summary>
    public class CursorAccessStrategy : IQueueAccessStrategy
    {
        private readonly MessageQueue _queue;
        private Cursor _cursor;
        private PeekAction _peekAction;
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1);
        private MessageQueueTransaction _writeOnlyTransaction;

        public CursorAccessStrategy(MessageQueue queue)
        {
            _queue = queue;
            _writeOnlyTransaction = new MessageQueueTransaction();
            _cursor = _queue.CreateCursor();
            _peekAction = PeekAction.Current;
        }

        public void BeginBatch()
        {
            _writeOnlyTransaction.Begin();
        }

        public void CommitBatch()
        {
            _writeOnlyTransaction.Commit();
        }

        public Message GetNext()
        {
            try
            {
                var message = _queue.Peek(Timeout, _cursor, _peekAction);
                _peekAction = PeekAction.Next;
                return message;
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
            messageQueue.Send(message,_writeOnlyTransaction);
        }

        public void Delete(Message message)
        {
            _queue.ReceiveById(message.Id,_writeOnlyTransaction);
        }

        public void Return(Message message)
        {
            // As original message has just been peeked, then do nothing
        }

        public void UndoGetNext(Message message)
        {
            // As original message has just been peeked, then do nothing
        }

        public void Dispose()
        {
            if (_cursor != null)
            {
                _cursor.Dispose();
                _cursor = null;
            }

            if (_writeOnlyTransaction != null)
            {
                _writeOnlyTransaction.Dispose();
                _writeOnlyTransaction = null;
            }
        }
    }
}