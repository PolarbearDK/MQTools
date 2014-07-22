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
        Label
    }

    public class MyMessageContext
    {
        private readonly MessageQueueTransaction _transaction;
        private readonly Message _message;
        private readonly Encoding _encoding;

        public MyMessageContext(MessageQueueTransaction transaction, Message message, Encoding encoding)
        {
            _transaction = transaction;
            _message = message;
            _encoding = encoding;
        }

        private bool _bodyStreamLoaded = false;
        private byte[] _body;

        public string GetBody()
        {
            if (!_bodyStreamLoaded)
            {
                _body = ((MemoryStream)_message.BodyStream).ToArray();
                _bodyStreamLoaded = true;
            }

            if (_body.Length > 0)
            {
                return _encoding.GetString(_body);
            }

            return null;
        }

        public string GetExtension()
        {
            var bytes = _message.Extension;
            if (bytes.Length > 0)
            {
                return _encoding.GetString(bytes);
            }
            return null;
        }

        private bool _headerLoaded = false;
        private HeaderInfo[] _headers;

        public HeaderInfo[] GetHeaders()
        {
            if (!_headerLoaded)
            {
                _headers = new HeaderInfo[] { };
                _headerLoaded = true;

                var bytes = _message.Extension;
                using (var mr = new MemoryStream(bytes))
                {
                    try
                    {
                        var ser = new XmlSerializer(typeof (HeaderInfo[]));
                        _headers = (HeaderInfo[]) ser.Deserialize(mr);
                    }
// ReSharper disable once EmptyGeneralCatchClause
                    catch {}
                }
            }

            return _headers;
        }

        public string Get(MessagePart part)
        {
            switch(part)
            {
                case MessagePart.Body:
                    return GetBody();
                case MessagePart.Extension:
                    return GetExtension();
                case MessagePart.Label:
                    return _message.Label;
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
            if (_bodyStreamLoaded)
            {
                _message.BodyStream = new MemoryStream(_body);
            }

            if(!isReturn)
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

        public DateTime? GetUtcSentTime()
        {
            var headers = GetHeaders();
            var header = headers.SingleOrDefault(x => x.Key == Headers.TimeSent);
            if (header != null)
            {
                try
                {
                    return DateTimeExtensions.ToUtcDateTime(header.Value);
                }
                catch (Exception)
                {
                }
            }
            return null;
        }
    }
}