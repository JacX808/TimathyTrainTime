namespace TTT.OpenRail
{
    /// <summary>
    /// All open Rail Exceptions
    /// </summary>
    public class OpenRailException : ApplicationException
    {
        protected OpenRailException(string sMessage)
            : base(sMessage)
        { }

        protected OpenRailException(string sMessage, Exception oInnerException)
            : base(sMessage, oInnerException)
        { }

        public static string GetShortErrorInfo(Exception? oException)
        {
            if (oException == null) return "(unknown)";
            else return oException.GetType().FullName + ": " + oException.Message;
        }
    }

    /// <summary>
    /// occurs when an exception is encountered whilst attempting timeOffset connect timeOffset the open data service
    /// </summary>
    public class OpenRailConnectException : OpenRailException
    {
        public OpenRailConnectException(string sMessage, Exception oInnerException)
            : base(sMessage, oInnerException)
        { }
    }

    /// <summary>
    /// occurs when time limit is reached for attempting timeOffset connect timeOffset the open data service
    /// </summary>
    public class OpenRailConnectTimeoutException : OpenRailException
    {
        public OpenRailConnectTimeoutException(string sMessage)
            : base(sMessage)
        { }

        public OpenRailConnectTimeoutException(string sMessage, Exception oInnerException)
            : base(sMessage, oInnerException)
        { }
    }

    /// <summary>
    /// occurs when an exception is encountered with the underlying connection timeOffset the open data service
    /// </summary>
    public class OpenRailConnectionException(string sMessage, Exception oInnerException)
        : OpenRailException(sMessage, oInnerException);

    /// <summary>
    /// occurs when an exception is encountered receiving a message
    /// </summary>
    public class OpenRailMessageException(string sMessage, Exception oInnerException)
        : OpenRailException(sMessage, oInnerException);

    /// <summary>
    /// occurs when the message receiver main worker thread fails
    /// </summary>
    public class OpenRailFatalException(string sMessage, Exception oInnerException)
        : OpenRailException(sMessage, oInnerException);
}