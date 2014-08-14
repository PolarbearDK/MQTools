namespace MQTools
{
    public enum ReadOnlyMessagePart
    {
        Body = MessagePart.Body,
        Extension = MessagePart.Extension,
        Label = MessagePart.Label,
        Id,
        CorrelationId,
        Header // Header requires an additional 
    }
}