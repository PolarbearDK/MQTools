using System;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Xml.Serialization;
using NServiceBus;
using NServiceBus.Transports.Msmq;

namespace MQTools
{
    public enum MessagePart
    {
        Body,
        Extension,
        Label,
    }

    public enum ReadOnlyMessagePart
    {
        Body = MessagePart.Body,
        Extension = MessagePart.Extension,
        Label = MessagePart.Label,
        Id,
        CorrelationId,
    }

    public class MyMessageContext
    {
        private readonly MessageQueueTransaction _transaction;
        private readonly Message _message;
        private readonly Encoding _encoding;
        private byte[] _body;
        private Lazy<string> _extensionLazy;
        private Lazy<HeaderInfo[]> _headersLazy;

        public MyMessageContext(MessageQueueTransaction transaction, Message message, Encoding encoding)
        {
            _transaction = transaction;
            _message = message;
            _encoding = encoding;
            _body = ((MemoryStream)_message.BodyStream).ToArray();
            ExtensionInitLazy(_message.Extension);
        }

        private void ExtensionInitLazy(byte[] bytes)
        {
            _extensionLazy = new Lazy<string>(() => GetStringFromBytes(bytes));
            _headersLazy = new Lazy<HeaderInfo[]>(() => GetHeaders(bytes));
        }

        private string GetStringFromBytes(byte[] bytes)
        {
            return bytes.Length > 0
                ? _encoding.GetString(bytes)
                : null;
        }

        private static HeaderInfo[] GetHeaders(byte[] extension)
        {
            using (var mr = new MemoryStream(extension))
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(HeaderInfo[]));
                    return (HeaderInfo[])serializer.Deserialize(mr);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }

            return new HeaderInfo[] {};
        }

        public string Get(ReadOnlyMessagePart part)
        {
            switch (part)
            {
                case ReadOnlyMessagePart.Body:
                    return GetStringFromBytes(_body);
                case ReadOnlyMessagePart.Extension:
                    return _extensionLazy.Value;
                case ReadOnlyMessagePart.Label:
                    return _message.Label;
                case ReadOnlyMessagePart.Id:
                    return _message.Id;
                case ReadOnlyMessagePart.CorrelationId:
                    return _message.CorrelationId;
                default:
                    throw new ArgumentOutOfRangeException("part");
            }
        }

        public void Set(string data, MessagePart part)
        {
            switch (part)
            {
                case MessagePart.Body:
                    _body = _encoding.GetBytes(data);
                    break;
                case MessagePart.Extension:
                    _message.Extension = _encoding.GetBytes(data);
                    ExtensionInitLazy(_message.Extension);
                    break;
                case MessagePart.Label:
                    _message.Label = data;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("part");
            }
        }

        public void Move(MessageQueue queue)
        {
            Send(queue, false);
        }

        public void Return(MessageQueue queue)
        {
            Send(queue, true);
        }

        private void Send(MessageQueue queue, bool isReturn)
        {
            _message.BodyStream = new MemoryStream(_body);

            if (!isReturn)
                Console.WriteLine("Moved message with ID {0} to queue {1}", _message.Id, queue.QueueName);

            try
            {
                queue.Send(_message, _transaction);
            }
            catch (MessageQueueException)
            {
                Console.Error.WriteLine("Exception thrown while sending to queue {0}", queue.FormatName);
                throw;
            }
        }

        public void SendClone(MessageQueue queue)
        {
            var message = new Message
                          {
                              Formatter = _message.Formatter,
                              AppSpecific = _message.AppSpecific,
                              BodyStream = new MemoryStream(_body),
                              CorrelationId = _message.CorrelationId,
                              Extension = _message.Extension,
                              Label = _message.Label,
                              ResponseQueue = _message.ResponseQueue
                          };

            try
            {
                queue.Send(message, _transaction);
            }
            catch (MessageQueueException)
            {
                Console.Error.WriteLine("Exception thrown while cloning to queue {0}", queue.FormatName);
                throw;
            }

            Console.WriteLine("Cloned message with ID {0} to queue {1}", message.Id, queue.QueueName);
        }

        /// <summary>
        /// Get NServiceBus sent time.
        /// </summary>
        /// <returns></returns>
        public DateTime? GetUtcSentTime()
        {
            var header = _headersLazy.Value.SingleOrDefault(x => x.Key == Headers.TimeSent);
            if (header != null)
            {
                try
                {
                    return DateTimeExtensions.ToUtcDateTime(header.Value);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }
            return null;
        }
    }
}