using System;
using System.Messaging;
using System.Text;
using Miracle.Arguments;
using System.Diagnostics;
using MQTools.QueueAccessStrategies;

namespace MQTools
{
	static class Program
	{
		static int Main(string[] args)
		{
			var arguments = args.ParseCommandLine<Arguments>();
			if (arguments == null)
				return 1;

		    var processor = new MessageProcessor(
                arguments.Commands, 
                Math.Max(arguments.BatchSize, 1),
                arguments.ReportInterval.GetValueOrDefault(1000),
                arguments.MaxMessages.GetValueOrDefault(uint.MaxValue),
                Encoding.GetEncoding(arguments.Encoding)
                );
		    var returnCode = 0;
            var queue = QueueFactory.GetInputQueue(arguments.SourceMachine, arguments.SourceQueue);

		    var stopwatch = Stopwatch.StartNew();
		    try
		    {
                using (var strategy = QueueAccessStrategyFactory.Create(arguments.QueueAccessStrategy, queue))
                {
                    processor.Process(strategy);
                }
		    }
		    catch (MessageQueueException ex)
		    {
		        Console.Error.WriteLine(ex);
		        returnCode = 10;
		    }
            stopwatch.Stop();

            Console.WriteLine("{0:#,##0} messages processed in {1} ({2:#,##0.00} msg/s).",
		                        processor.MessagesProcessed,
		                        stopwatch.Elapsed,
		                        processor.MessagesProcessed/Math.Max(stopwatch.Elapsed.TotalSeconds, 1)
		        );

			return returnCode;
		}
	}
}
