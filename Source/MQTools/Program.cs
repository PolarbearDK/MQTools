using System;
using System.Messaging;
using System.Text;
using Miracle.Arguments;
using System.Diagnostics;

namespace MQTools
{
	class Program
	{
		static int Main(string[] args)
		{
			var arguments = args.ParseCommandLine<Arguments>();
			if (arguments == null)
				return 1;

		    var queue = QueueFactory.GetQueue(arguments.SourceMachine, arguments.SourceQueue);
		    var processor = new QueueProcessor(queue, arguments.Commands, Encoding.GetEncoding(arguments.Encoding));
		    var returnCode = 0;
		    

		    var stopwatch = Stopwatch.StartNew();
		    try
		    {
                processor.MessageLoop(
                    Math.Max(arguments.BatchSize, 1), 
                    arguments.MaxMessages.GetValueOrDefault(uint.MaxValue));
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
