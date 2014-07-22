using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Text;

namespace MQTools
{
    public class QueueProcessor
    {
        private readonly MessageQueue _queue;
        private readonly IEnumerable<CommandBase> _commands;
        private readonly Encoding _encoding;

        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1);
        public long MessagesProcessed { get; private set; }
        public DateTime CutoffDate { get; private set; }

        public QueueProcessor(MessageQueue queue, IEnumerable<CommandBase> commands, Encoding encoding)
        {
            if (queue == null) throw new ArgumentNullException("queue");
            _queue = queue;
            _queue.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
            var messagePropertyFilter = new MessagePropertyFilter();
            messagePropertyFilter.SetAll();
            queue.MessageReadPropertyFilter = messagePropertyFilter;

            foreach (var command in commands)
            {
                command.Initialize();
            }
            _commands = commands;
            _encoding = encoding;
        }

        public void MessageLoop(uint batchSize, uint maxMessages)
        {
            // Remember when we started, to prevent processing the same messages several times
            CutoffDate = DateTime.Now.Subtract(TimeSpan.FromSeconds(1));
            MessagesProcessed = 0;

            while (true)
            {
                using (var transaction = new MessageQueueTransaction())
                {
                    transaction.Begin();
                    for (int batch = 0; batch < batchSize; batch++)
                    {
                        Message message;
                        bool done;

                        if (MessagesProcessed >= maxMessages)
                        {
                            transaction.Commit();
                            return; // Exit message loop
                        }

                        try
                        {
                            message = _queue.Receive(Timeout, transaction);
                            done = message.SentTime >= CutoffDate;
                        }
                        catch (MessageQueueException ex)
                        {
                            if (ex.Message.Contains("Timeout"))
                            {
                                message = null;
                                done = true;
                            }
                            else if (ex.Message.Contains("The queue does not exist"))
                            {
                                Console.Error.WriteLine("Unable to open the input queue: {0}", _queue.FormatName);
                                message = null;
                                done = true;
                            }
                            else
                                throw;
                        }

                        if (done)
                        {
                            if (batch > 0)
                            {
                                // Other messages participate in this transaction. Send to end of queue and commit
                                if(message != null)
                                    _queue.Send(message, transaction);

                                transaction.Commit();
                            }
                            else
                            {
                                // This message is the only one in transaction. It is safe to rollback
                                transaction.Abort();
                            }

                            return; // Exit message loop
                        }

                        MessagesProcessed++;

                        var context = new MyMessageContext(transaction, message, _encoding);
                        if (!Process(context))
                        {
                            context.Return(_queue);
                        }
                    }
                    transaction.Commit();
                    Console.WriteLine("{0:#,##0} messages processed.", MessagesProcessed);
                }
            }
        }

        private bool Process(MyMessageContext context)
        {
            return _commands
                .Where(command => command.Match(context))
                .Any(command => command.Process(context));
        }
    }
}