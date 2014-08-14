using System;
using System.Linq;
using System.Messaging;
using System.Text.RegularExpressions;
using Miracle.Arguments;
using MQTools.QueueAccessStrategies;
using NServiceBus;

namespace MQTools
{
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    // ReSharper disable UnusedMember.Global
    // ReSharper disable MemberCanBeProtected.Global

    /// <summary>
    /// Sample argument class that shows examples of most of the functionality of the CommandLineParser.
    /// </summary>
    [ArgumentSettings(
        ArgumentNameComparison = StringComparison.InvariantCultureIgnoreCase,
        DuplicateArgumentBehaviour = DuplicateArgumentBehaviour.Unknown,
        StartOfArgument = new[] { '-' },
        ValueSeparator = new[] { ' ' },
        ShowHelpOnArgumentErrors = false
        )]
    [ArgumentDescription("MSMQ Queue Tools.")]
    public class Arguments
    {
        public Arguments()
        {
            BatchSize = 1;
            Encoding = "UTF-8";
            QueueAccessStrategy = QueueAccessStrategy.Cursor;
        }

        [ArgumentName("SourceQueue", "Source", "SQ")]
        [ArgumentRequired]
        [ArgumentDescription("Name of source queue. Supports NServiceBus queue names (queue@server)")]
        public string SourceQueue { get; set; }

        [ArgumentName("SourceMachine", "SM", "M")]
        [ArgumentDescription("Server that MSMQ source queue resides on.")]
        public string SourceMachine { get; set; }

        [ArgumentName("Encoding", "ENC")]
        [ArgumentDescription("Text encoding of the message. Must match the name of a .NET Encoding. Default: UTF-8")]
        public string Encoding { get; set; }

        [ArgumentName("BatchSize", "Batch", "B")]
        [ArgumentDescription("How many messages per transaction. Default is to handle 1 message per transaction (slow). Increasing batch size greatly increases throughput.")]
        public uint BatchSize { get; set; }

        [ArgumentName("MaxMessages", "Max")]
        [ArgumentDescription("Maximum number of messages to process. Default is all messages.")]
        public uint? MaxMessages { get; set; }

        [ArgumentName("ReportInterval", "Report", "Interval")]
        [ArgumentDescription("Determines the interval between status reports. Default is after every 1000 messages. 0=off.")]
        public long? ReportInterval { get; set; }

        [ArgumentName("QueueAccessStrategy", "QueueAccess", "Access")]
        [ArgumentDescription("Determines the strategy used to get messages from source queue. Cursor is faster, does not change ID of messages, and can read from remote queues. Receive is better when multiple processes access the same local queue. Default is Cursor.")]
        public QueueAccessStrategy QueueAccessStrategy { get; set; }

        [ArgumentCommand(typeof(CopyCommand), "Copy", "CP")]
        [ArgumentCommand(typeof(MoveCommand), "Move", "MV")]
        [ArgumentCommand(typeof(DeleteCommand), "Delete", "DEL")]
        [ArgumentCommand(typeof(PrintCommand), "Print", "Echo")]
        [ArgumentCommand(typeof(CountCommand), "Count")]
        [ArgumentCommand(typeof(AlterCommand), "Alter", "Change")]
        [ArgumentRequired]
        public CommandBase[] Commands { get; set; }

        [ArgumentName("Help", "H", "?")]
        [ArgumentHelp]
        [ArgumentDescription(@"Display help.")]
        public bool Help { get; set; }

        [ArgumentName("CommandHelp", "CH", "??")]
        [ArgumentCommandHelp]
        [ArgumentDescription(@"Display help for command.")]
        public string Command { get; set; }
    }


    public abstract class CriteriaBase
    {
        protected CriteriaBase()
        {
            StringComparison = StringComparison.CurrentCultureIgnoreCase;
        }

        public virtual void Initialize()
        {
        }

        public void Cleanup()
        {
        }

        public abstract bool Match(MessageContext context, ReadOnlyMessagePart messagePart, string headerKey);

        [ArgumentName("StringComparison")]
        [ArgumentDescription("StringComparison used for string compares. Default is CurrentCultureIgnoreCase.")]
        public StringComparison StringComparison { get; set; }
    }

    [ArgumentDescription("Check that message part matches a regular expression")]
    public class MatchCriteria : CriteriaBase
    {
        protected Regex Regex;

        [ArgumentPosition(0)]
        [ArgumentRequired]
        public string Pattern { get; set; }

        // In prepare convert pattern into compiled regular expressions
        public override void Initialize()
        {
            Regex = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        public override bool Match(MessageContext context, ReadOnlyMessagePart messagePart, string headerKey)
        {
            return Regex.IsMatch(context.Get(messagePart, headerKey));
        }
    }

    [ArgumentDescription(@"Check that message part is like a simple ""DOS"" wildcard expression (* and ?)")]
    public class LikeCriteria : MatchCriteria
    {
        // In prepare convert pattern into compiled regular expressions
        public override void Initialize()
        {
            Regex = new Wildcard(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }
    }

    [ArgumentDescription("Check that NSB send time is older than specified timespan")]
    public class OlderThanCriteria : CriteriaBase
    {
        private DateTime _utcCompareTime;

        [ArgumentPosition(0)]
        [ArgumentRequired]
        public TimeSpan OlderThan { get; set; }

        public override void Initialize()
        {
            _utcCompareTime = DateTime.UtcNow - OlderThan;
        }

        /// <summary>
        /// Compare NServiceBus sent time (header value) .
        /// </summary>
        /// <returns></returns>
        public override bool Match(MessageContext context, ReadOnlyMessagePart messagePart, string headerKey)
        {
            var header = context.GetHeader(headerKey ?? Headers.TimeSent);
            if (header == null) return false;

            try
            {
                var sentTime = DateTimeExtensions.ToUtcDateTime(header.Value);
                return sentTime < _utcCompareTime;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [ArgumentDescription("Check that message part contains a string")]
    public class ContainsCriteria : CriteriaBase
    {
        [ArgumentPosition(0)]
        [ArgumentRequired]
        public string Contains { get; set; }

        public override bool Match(MessageContext context, ReadOnlyMessagePart messagePart, string headerKey)
        {
            var part = context.Get(messagePart,headerKey);

            return part.IndexOf(Contains, StringComparison) != -1;
        }
    }

    [ArgumentDescription("Check that message part contains a string")]
    public class EqualsCriteria : CriteriaBase
    {
        [ArgumentPosition(0)]
        [ArgumentRequired]
        public string EqualsValue { get; set; }

        public override bool Match(MessageContext context, ReadOnlyMessagePart messagePart, string headerKey)
        {
            var part = context.Get(messagePart, headerKey);

            return part.Equals(EqualsValue, StringComparison);
        }
    }

    public class WhereCommand
    {
        public WhereCommand()
        {
            Part = ReadOnlyMessagePart.Body;
        }

        [ArgumentPosition(0)]
        [ArgumentDescription("Which part of message to check. Default is body.")]
        public ReadOnlyMessagePart Part { get; set; }

        [ArgumentName("HeaderKey","Key")]
        [ArgumentDescription("Header Key if part=Header, otherwise ignored.")]
        public string HeaderKey { get; set; }

        [ArgumentName("Not")]
        public bool Negate { get; set; }

        [ArgumentCommand(typeof(LikeCriteria), "Like")]
        [ArgumentCommand(typeof(MatchCriteria), "Match", "Matches")]
        [ArgumentCommand(typeof(ContainsCriteria), "Contain", "Contains")]
        [ArgumentCommand(typeof(OlderThanCriteria), "OlderThan", "Older")]
        [ArgumentCommand(typeof(EqualsCriteria), "Equal", "Equals","Eq","=","==")]
        [ArgumentRequired]
        public CriteriaBase Criteria { get; set; }

        public void Initialize()
        {
            Criteria.Initialize();
        }

        public void Cleanup()
        {
            Criteria.Cleanup();
        }

        public bool Match(MessageContext context)
        {
            return Criteria.Match(context, Part, HeaderKey) == !Negate;
        }
    }

    public abstract class CommandBase
    {
        [ArgumentCommand(typeof(WhereCommand), "Where", "And")]
        public WhereCommand[] Filters { get; set; }

        public bool Match(MessageContext context)
        {
            return Filters == null || Filters.All(x => x.Match(context));
        }

        public virtual void Initialize()
        {
            if (Filters != null)
            {
                foreach (var filter in Filters)
                {
                    filter.Initialize();
                }
            }
        }

        public virtual void Cleanup()
        {
            if (Filters != null)
            {
                foreach (var filter in Filters)
                {
                    filter.Cleanup();
                }
            }
        }

        public virtual bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            return false; // Message has not been handled
        }
    }

    public abstract class QueueCommandBase : CommandBase
    {
        protected MessageQueue DestinationQueue { get; private set; }
        protected string DestinationQueueName { get; private set; }

        [ArgumentName("ToQueue", "To")]
        [ArgumentRequired]
        public string To { get; set; }

        [ArgumentName("OnServer", "On")]
        public string On { get; set; }

        public override void Initialize()
        {
            DestinationQueueName = QueueFactory.GetQueueName(On, To);
            DestinationQueue = QueueFactory.GetOutputQueue(On, To);
            base.Initialize();
        }


        public override void Cleanup()
        {
            if (DestinationQueue != null)
            {
                DestinationQueue.Dispose();
                DestinationQueue = null;
            }
            base.Cleanup();
        }
    }

    [ArgumentDescription("Copy message to queue.")]
    public class CopyCommand : QueueCommandBase
    {
        public override bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            strategy.Send(DestinationQueue, context.GetClonedMessage());
            Console.WriteLine("Cloned message with ID {0} to queue {1}", context.Id, DestinationQueueName);
            return false;
        }
    }

    [ArgumentDescription("Move message to queue.")]
    public class MoveCommand : QueueCommandBase
    {
        public override bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            strategy.Send(DestinationQueue, context.GetMessage());
            Console.WriteLine("Moved message with ID {0} to queue {1}", context.Id, DestinationQueueName);
            return true; // Message has been handled
        }
    }

    [ArgumentDescription("Delete message.")]
    public class DeleteCommand : CommandBase
    {
        public override bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            strategy.Delete(context.GetMessage());
            return true; // Message has been handled
        }
    }

    [ArgumentDescription("Print message part as raw text.")]
    public class PrintCommand : CommandBase
    {
        public PrintCommand()
        {
            Part = ReadOnlyMessagePart.Body;
        }

        [ArgumentPosition(0)]
        [ArgumentDescription("Which part of message to print. Default is body.")]
        public ReadOnlyMessagePart Part { get; set; }

        [ArgumentPosition(1)]
        [ArgumentDescription("Header Key if part=Header, otherwise ignored.")]
        public string HeaderKey { get; set; }

        public override bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            Console.WriteLine(context.Get(Part, HeaderKey));
            return base.PerformAction(strategy, context);
        }
    }

    [ArgumentDescription("Print message part as raw text.")]
    public class CountCommand : CommandBase
    {
        private static int uniqueCounterNumber = 1;
        private int count;

        public CountCommand()
        {
            Name = "Counter" + uniqueCounterNumber++;
        }

        public override void Initialize()
        {
            count = 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            Console.WriteLine("{0}: {1}", Name, count);
        }

        [ArgumentName("Name","As")]
        [ArgumentDescription("Name of counter.")]
        public string Name { get; set; }

        public override bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            count++;
            return base.PerformAction(strategy, context);
        }
    }

    [ArgumentDescription("Do search and replace in message.")]
    public class AlterCommand : CommandBase
    {
        public AlterCommand()
        {
            Part = MessagePart.Body;
        }

        [ArgumentPosition(0)]
        public MessagePart Part { get; set; }

        [ArgumentName("Search","Find")]
        [ArgumentRequired]
        public string Search { get; set; }

        [ArgumentName("Replace")]
        [ArgumentRequired]
        public string Replace { get; set; }

        public override bool PerformAction(IQueueAccessStrategy strategy, MessageContext context)
        {
            context.Set(context.Get((ReadOnlyMessagePart)Part,null).Replace(Search,Replace), Part);
            return base.PerformAction(strategy, context);
        }
    }

    // ReSharper restore MemberCanBePrivate.Global
    // ReSharper restore UnusedAutoPropertyAccessor.Global
    // ReSharper restore UnusedMember.Global
    // ReSharper restore MemberCanBeProtected.Global
}