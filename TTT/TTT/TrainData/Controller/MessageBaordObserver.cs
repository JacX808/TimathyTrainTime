using TTT.OpenRail;

namespace TTT.TrainData.Controller;

public class MessageBaordObserver : BackgroundService
{
    private readonly ILogger<MessageBaordObserver> _log;
    private readonly OpenRailNRODReceiver _receiver;

    public MessageBaordObserver(
        ILogger<MessageBaordObserver> log,
        OpenRailNRODReceiver receiver)
    {
        _log = log;
        _receiver = receiver;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(ListenForMessagesAsync);
    }

    private async Task ListenForMessagesAsync()
    {
        DateTime dtRunUntilUtc = DateTime.UtcNow.AddSeconds(120);
        DateTime dtNextUiUpdateTime = DateTime.UtcNow;
        int iTextMessageCount1 = 0;
        int iBytesMessageCount1 = 0;
        int iUnsupportedMessageCount1 = 0;
        string msLastTextMessage1 = null;
        int iTextMessageCount2 = 0;
        int iBytesMessageCount2 = 0;
        int iUnsupportedMessageCount2 = 0;
        string msLastTextMessage2 = null;
        int iErrorCount = 0;
        string msLastErrorInfo = null;
        while (DateTime.UtcNow < dtRunUntilUtc)
        {
            // attempt to dequeue and process any errors that occurred in the receiver
            while ((_receiver.moErrorQueue.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
            {
                OpenRailException oOpenRailException = null;
                if (_receiver.moErrorQueue.TryDequeue(out oOpenRailException))
                {
                    // the code here simply counts the errors, and captures the details of the last 
                    // error - your code may log details of errors to a database or log file
                    iErrorCount++;
                    msLastErrorInfo = OpenRailException.GetShortErrorInfo(oOpenRailException);
                }
            }

            // attempt to dequeue and process some messages
            while ((_receiver.moMessageQueue1.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
            {
                OpenRailMessage oMessage = null;
                if (_receiver.moMessageQueue1.TryDequeue(out oMessage))
                {
                    // All Network Rail Open Data Messages should be text
                    OpenRailTextMessage oTextMessage = oMessage as OpenRailTextMessage;
                    if (oTextMessage != null)
                    {
                        iTextMessageCount1++;
                        msLastTextMessage1 = oTextMessage.Text;
                    }

                    // Network Rail Open Data Messages should not be bytes messages (code is here just in case)
                    OpenRailBytesMessage oBytesMessage = oMessage as OpenRailBytesMessage;
                    if (oBytesMessage != null) iBytesMessageCount1++;

                    // All Network Rail Open Data Messages should be text (code is here just in case)
                    OpenRailUnsupportedMessage oUnsupportedMessage = oMessage as OpenRailUnsupportedMessage;
                    if (oUnsupportedMessage != null) iUnsupportedMessageCount1++;
                }
            }

            while ((_receiver.moMessageQueue2.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
            {
                OpenRailMessage oMessage = null;
                if (_receiver.moMessageQueue2.TryDequeue(out oMessage))
                {
                    // All Network Rail Open Data Messages should be text
                    OpenRailTextMessage oTextMessage = oMessage as OpenRailTextMessage;
                    if (oTextMessage != null)
                    {
                        iTextMessageCount2++;
                        msLastTextMessage2 = oTextMessage.Text;
                    }

                    // Network Rail Open Data Messages should not be bytes messages (code is here just in case)
                    OpenRailBytesMessage oBytesMessage = oMessage as OpenRailBytesMessage;
                    if (oBytesMessage != null) iBytesMessageCount2++;

                    // All Network Rail Open Data Messages should be text (code is here just in case)
                    OpenRailUnsupportedMessage oUnsupportedMessage = oMessage as OpenRailUnsupportedMessage;
                    if (oUnsupportedMessage != null) iUnsupportedMessageCount2++;
                }
            }

            if (dtNextUiUpdateTime < DateTime.UtcNow)
            {
                Console.Clear();
                Console.WriteLine("NETWORK RAIL OPEN DATA RECEIVER SAMPLE: ");
                Console.WriteLine();
                Console.WriteLine("Remaining Run Time = " +
                                  dtRunUntilUtc.Subtract(DateTime.UtcNow).TotalSeconds.ToString("###0.0") + " seconds");
                Console.WriteLine();
                Console.WriteLine("Receiver Status:");
                Console.WriteLine("  Running = " + _receiver.IsRunning.ToString() + ", Connected To Data Feed = " +
                                  _receiver.IsConnected.ToString());
                Console.WriteLine("  Size of local In-Memory Queue 1 (" + _receiver.msTopic1 + ") = " +
                                  _receiver.moMessageQueue1.Count
                                      .ToString()); // i.e. messages received from the feed but not yet processed locally
                Console.WriteLine("  Size of local In-Memory Queue 2 (" + _receiver.msTopic2 + ") = " +
                                  _receiver.moMessageQueue2.Count
                                      .ToString()); // i.e. messages received from the feed but not yet processed locally
                Console.WriteLine("  Last Message Received At = " + _receiver.LastMessageReceivedAtUtc.ToLocalTime()
                    .ToString("HH:mm:ss.fff ddd dd MMM yyyy"));
                Console.WriteLine("  Msg Counts:  (1: {0}) = {1}, (2: {2}) = {3}", _receiver.msTopic1, _receiver.MessageCount1,
                    _receiver.msTopic2, _receiver.MessageCount2);
                Console.WriteLine();
                Console.WriteLine("Processing Status 1 (" + _receiver.msTopic1 + "):");
                Console.WriteLine("  Msg Counts: Text = {0}, Bytes = {1}, Unsupported = {2}", iTextMessageCount1,
                    iBytesMessageCount1, iUnsupportedMessageCount1);
                Console.WriteLine("  Last JSON = " + (msLastTextMessage1 == null
                    ? ""
                    : (msLastTextMessage1.Length > 40
                        ? msLastTextMessage1.Substring(0, 40) + "..."
                        : msLastTextMessage1)));
                Console.WriteLine();
                Console.WriteLine("Processing Status 2 (" + _receiver.msTopic2 + "):");
                Console.WriteLine("  Msg Counts: Text = {0}, Bytes = {1}, Unsupported = {2}", iTextMessageCount2,
                    iBytesMessageCount2, iUnsupportedMessageCount2);
                Console.WriteLine("  Last JSON = " + (msLastTextMessage2 == null
                    ? ""
                    : (msLastTextMessage2.Length > 40
                        ? msLastTextMessage2.Substring(0, 40) + "..."
                        : msLastTextMessage2)));
                Console.WriteLine();
                Console.WriteLine("Errors:  Total Errors = " + iErrorCount.ToString());
                Console.WriteLine("  Last Error = " + (msLastErrorInfo == null ? "" : msLastErrorInfo));
                Console.WriteLine();
                dtNextUiUpdateTime = DateTime.UtcNow.AddMilliseconds(500);
            }

            if ((_receiver.moMessageQueue1.Count < 10) && (_receiver.moMessageQueue2.Count < 10)) Thread.Sleep(50);
        }

        Console.WriteLine("Stopping Receiver...");

        _receiver.RequestStop();

        while (_receiver.IsRunning)
        {
            Thread.Sleep(50);
        }

        Console.WriteLine("Receiver stopped.");
        Console.WriteLine("Finished.");
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }
}