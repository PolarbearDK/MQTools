using System;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Xml.Serialization;
using NServiceBus.Transports.Msmq;

namespace MQTools
{
    public class MessageContext
    {
        private readonly Message _message;
        private readonly Encoding _encoding;
        private byte[] _body;
        private Lazy<string> _extensionLazy;
        private Lazy<HeaderInfo[]> _headersLazy;

        public MessageContext(Message message, Encoding encoding)
        {
            _message = message;
            _encoding = encoding;
            _body = ((MemoryStream)_message.BodyStream).ToArray();
            ExtensionInitLazy(_message.Extension);
        }

        public string Id    
        {
            get { return _message.Id; }
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

            return new HeaderInfo[] { };
        }

        public string Get(ReadOnlyMessagePart part, string key)
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
                case ReadOnlyMessagePart.Header:
                    var header = GetHeader(key);
                    return header != null ? header.Value : null;
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

        public HeaderInfo GetHeader(string key)
        {
            return _headersLazy.Value.SingleOrDefault(x => x.Key == key);
        }

        public Message GetMessage()
        {
            _message.BodyStream = new MemoryStream(_body);
            return _message;
        }

        /// <summary>
        /// Clone current message
        /// </summary>
        /// <returns></returns>
        public Message GetClonedMessage()
        {
            return new Message
            {
                Formatter = _message.Formatter,
                AppSpecific = _message.AppSpecific,
                BodyStream = new MemoryStream(_body),
                CorrelationId = _message.CorrelationId,
                Extension = _message.Extension,
                Label = _message.Label,
                ResponseQueue = _message.ResponseQueue
            };
        }
    }
}