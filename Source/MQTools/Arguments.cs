using System;
using System.Linq;
using System.Messaging;
using System.Text.RegularExpressions;
using Miracle.Arguments;

namespace MQTools
{
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

        [ArgumentCommand(typeof(CopyCommand), "Copy", "CP")]
        [ArgumentCommand(typeof(MoveCommand), "Move", "MV")]
        [ArgumentCommand(typeof(DeleteCommand), "Delete", "DEL")]
        [ArgumentCommand(typeof(PrintCommand), "Print", "Echo")]
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

        public virtual void Cleanup()
        {
        }

        public abstract bool Match(MyMessageContext context, MessagePart messagePart);

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

        public override bool Match(MyMessageContext context, MessagePart messagePart)
        {
            return Regex.IsMatch(context.Get(messagePart));
        }
    }

    [ArgumentDescription("Check that message part is like a simple wildcard expression")]
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
            _utcCompareTime = DateTime.UtcNow;
        }

        public override bool Match(MyMessageContext context, MessagePart messagePart)
        {
            var sentTime = context.GetUtcSentTime();
            if (sentTime != null)
            {
                return (_utcCompareTime - sentTime.Value) > OlderThan;
            }
            return false;
        }
    }

    [ArgumentDescription("Check that message contains a string")]
    public class ContainsCriteria: CriteriaBase
    {
        [ArgumentPosition(0)]
        [ArgumentRequired]
        public string Contains { get; set; }

        public override bool Match(MyMessageContext context, MessagePart messagePart)
        {
            var part = context.Get(messagePart);

            return part.IndexOf(Contains, StringComparison) != -1;
        }
    }

    public class WhereCommand
    {
        public WhereCommand()
        {
            Part = MessagePart.Body;
        }

        [ArgumentPosition(0)]
        public MessagePart Part { get; set; }

        [ArgumentName("Not")]
        public bool Not { get; set; }

        [ArgumentCommand(typeof(LikeCriteria), "Like")]
        [ArgumentCommand(typeof(MatchCriteria), "Match", "Matches")]
        [ArgumentCommand(typeof(ContainsCriteria), "Contain", "Contains")]
        [ArgumentCommand(typeof(OlderThanCriteria), "OlderThan", "Older")]
        [ArgumentRequired]
        public CriteriaBase Criteria { get; set; }

        public virtual void Initialize()
        {
            Criteria.Initialize();
        }

        public virtual void Cleanup()
        {
            Criteria.Cleanup();
        }

        public bool Match(MyMessageContext context)
        {
            return Criteria.Match(context, Part) 
                ? Not == false 
                : Not == true;
        }
    }

    public abstract class CommandBase
    {
        [ArgumentCommand(typeof(WhereCommand), "Where", "And")]
        public WhereCommand[] Filters { get; set; }

        public virtual bool Match(MyMessageContext context)
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

        public virtual bool Process(MyMessageContext context)
        {
            return false; // Message has not been handled
        }
    }

    public abstract class QueueCommandBase : CommandBase
    {
        protected MessageQueue DestinationQueue;

        [ArgumentName("ToQueue", "To")]
        [ArgumentRequired]
        public string To { get; set; }

        [ArgumentName("OnServer", "On")]
        public string On { get; set; }

        public override void Initialize()
        {
            DestinationQueue = QueueFactory.GetQueue(On, To);
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
        public override bool Process(MyMessageContext context)
        {
            context.SendClone(DestinationQueue);
            return base.Process(context);
        }
    }

    [ArgumentDescription("Move message to queue.")]
    public class MoveCommand : QueueCommandBase
    {
        public override bool Process(MyMessageContext context)
        {
            context.Move(DestinationQueue);
            return true;
        }
    }

    [ArgumentDescription("Delete message.")]
    public class DeleteCommand : CommandBase
    {
        public override bool Process(MyMessageContext context)
        {
            return true;
        }
    }

    [ArgumentDescription("Print message part as raw text.")]
    public class PrintCommand : CommandBase
    {
        public PrintCommand()
        {
            Part = MessagePart.Body;
        }

        [ArgumentPosition(0)]
        [ArgumentDescription("Which part of message to print. Default is body.")]
        public MessagePart Part { get; set; }

        public override bool Process(MyMessageContext context)
        {
            Console.WriteLine(context.Get(Part));
            return base.Process(context);
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

        public override bool Process(MyMessageContext context)
        {
            context.Set(context.Get(Part).Replace(Search,Replace), Part);
            return base.Process(context);
        }
    }
}