namespace TTT.OpenRail
{
    public class OpenRailMessage
    {
        protected OpenRailMessage(DateTime dtNmsTimestamp)
        {
            NmsTimestamp = dtNmsTimestamp;
        }

        public DateTime NmsTimestamp { get; set; }
    }

    public class OpenRailTextMessage(DateTime dtNmsTimestamp, string sText) : OpenRailMessage(dtNmsTimestamp)
    {
        public string? Text { get; } = sText;
    }

    public class OpenRailBytesMessage(DateTime dtNmsTimestamp, byte[] bBytes) : OpenRailMessage(dtNmsTimestamp)
    {
        public byte[]? Bytes { get; } = bBytes;
    }

    public class OpenRailUnsupportedMessage(DateTime dtNmsTimestamp, string sMessageType)
        : OpenRailMessage(dtNmsTimestamp)
    {
        public string? UnsupportedMessageType { get; } = sMessageType;
    }
}