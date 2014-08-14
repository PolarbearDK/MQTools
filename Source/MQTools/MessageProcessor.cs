using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Text;
using MQTools.QueueAccessStrategies;

namespace MQTools
{
    public class MessageProcessor
    {
        private readonly IEnumerable<CommandBase> _commands;
        private readonly uint _batchSize;
        private readonly uint _maxMessages;
        private readonly long _reportInterval;
        private readonly Encoding _encoding;
        private readonly DateTime _cutoffDate;

        public long MessagesProcessed { get; private set; }

        public MessageProcessor(IEnumerable<CommandBase> commands, uint batchSize, long reportInterval, uint maxMessages, Encoding encoding)
        {
            _commands = commands;
            _batchSize = batchSize;
            _reportInterval = reportInterval;
            _maxMessages = maxMessages;
            _encoding = encoding;

            // Messages sent after this Date/Time terminates queue loop
            _cutoffDate = DateTime.Now.Subtract(TimeSpan.FromSeconds(1));
        }

        public void Process(IQueueAccessStrategy strategy)
        {
            MessagesProcessed = 0;

            foreach (var command in _commands)
            {
                command.Initialize();
            }

            while (ProcessBatch(strategy))
            {
            }

            foreach (var command in _commands)
            {
                command.Cleanup();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="strategy"></param>
        /// <returns>True when more to do. False when done.</returns>
        private bool ProcessBatch(IQueueAccessStrategy strategy)
        {
            strategy.BeginBatch();
            for (var batch = _batchSize; batch > 0; batch--)
            {
                var message = GetNextMessage(strategy);
                if (message == null)
                    return false;

                if (!Process(strategy, message))
                {
                    strategy.Return(message.GetMessage());
                }

                MessagesProcessed++;
                if (_reportInterval != 0 && MessagesProcessed % _reportInterval == 0)
                    Console.WriteLine("{0:#,##0} messages processed.", MessagesProcessed);
            }
            strategy.CommitBatch();

            return true;
        }

        private MessageContext GetNextMessage(IQueueAccessStrategy strategy)
        {
            if (MessagesProcessed >= _maxMessages)
            {
                strategy.CommitBatch();
                return null; // All done
            }

            Message message = strategy.GetNext();
            if (message == null)
            {
                strategy.CommitBatch();
                return null;
            }

            if (message.SentTime >= _cutoffDate)
            {
                strategy.UndoGetNext(message);
                return null;
            }

            return new MessageContext(message, _encoding);
        }

        private bool Process(IQueueAccessStrategy strategy, MessageContext context)
        {
            return _commands
                .Where(command => command.Match(context))
                .Any(command => command.PerformAction(strategy, context));
        }
    }
}