using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.AspNetCore.Connections;
using IConnectionFactory = Apache.NMS.IConnectionFactory;
using ISession = Apache.NMS.ISession;

namespace TTT.OpenRail
{
    /*
     * This sample illustrates how to use .Net generally and C# specifically to 
     * receive and process messages from the Network Rail Open Data Platform.  Originally written by Chris Bailiss.
     * This sample makes use of the Apache NMS Messaging API - http://activemq.apache.org/nms/
     * This sample was built against v1.5.1 of the API.  
     * The Apache.NMS and Apache.NMS.Stomp assemblies can be downloaded from http://activemq.apache.org/nms/download.html
     */

    public class OpenRailNRODReceiver
    {
        private IConnectionFactory moConnectionFactory = null;
        private IConnection moConnection = null;
        private ISession moSession = null;
        private ITopic moTopic1 = null;
        private ITopic moTopic2 = null;
        private IMessageConsumer moConsumer1 = null;
        private IMessageConsumer moConsumer2 = null;

        private string msConnectUrl = null;
        private string msUser = null;
        private string msPassword = null;
        private string msTopic1 = null;
        private string msTopic2 = null;
        private bool mbUseDurableSubscription = false;
        private int miAttemptToConnectForSeconds = 120;

        private ConcurrentQueue<OpenRailMessage> moMessageQueue1 = null;
        private ConcurrentQueue<OpenRailMessage> moMessageQueue2 = null;
        private ConcurrentQueue<OpenRailException> moErrorQueue = null;

        private CancellationTokenSource moCTS = null;
        private Task mtManagement = null;

        private long miSpinSpreadUntilUtc = (new DateTime(2000, 1, 1)).Ticks;
        private long miIsConnected = 0;
        private long miLastMessageReceivedAtUtc = (new DateTime(2000, 1, 1)).Ticks;
        private long miMessageReadCount1 = 0;
        private long miMessageReadCount2 = 0;
        private long miLastConnectionExceptionAtUtc = (new DateTime(2000, 1, 1)).Ticks;

        public OpenRailNRODReceiver(string sConnectUrl, string sUser, string sPassword, string sTopic1, string sTopic2,
            ConcurrentQueue<OpenRailMessage> oMessageQueue1, ConcurrentQueue<OpenRailMessage> oMessageQueue2, ConcurrentQueue<OpenRailException> oErrorQueue, 
            bool bUseDurableSubscription, int iAttemptToConnectForSeconds = 300)
        {
            msConnectUrl = sConnectUrl;
            msUser = sUser;
            msPassword = sPassword;
            msTopic1 = sTopic1;
            msTopic2 = sTopic2;
            moMessageQueue1 = oMessageQueue1;
            moMessageQueue2 = oMessageQueue2;
            moErrorQueue = oErrorQueue;
            mbUseDurableSubscription = bUseDurableSubscription;
            miAttemptToConnectForSeconds = iAttemptToConnectForSeconds;
        }

        public void Start()
        {
            lock (this)
            {
                if (IsRunning) throw new ApplicationException("OpenRailNRODReceiver already running!");
                moCTS = new CancellationTokenSource();
                mtManagement = Task.Run((Func<Task>)Run);
                mtManagement.ConfigureAwait(false);
            }
        }

        public bool IsRunning
        {
            get { return mtManagement == null ? false : !(mtManagement.IsCanceled || mtManagement.IsCompleted || mtManagement.IsFaulted); }
        }

        public bool IsConnected { get { return Interlocked.Read(ref miIsConnected) > 0; } }
        public long MessageCount1 { get { return Interlocked.Read(ref miMessageReadCount1); } }
        public long MessageCount2 { get { return Interlocked.Read(ref miMessageReadCount2); } }
        public Exception FatalException { get { return mtManagement.IsFaulted ? mtManagement.Exception : null; } }

        public void RequestStop()
        {
            if (moCTS != null) moCTS.Cancel();
        }

        private DateTime SpinSpreadUntilUtc
        {
            get { return new DateTime(Interlocked.Read(ref miSpinSpreadUntilUtc)); }
            set { Interlocked.Exchange(ref miSpinSpreadUntilUtc, value.Ticks); }
        }

        public DateTime LastMessageReceivedAtUtc
        {
            get { return new DateTime(Interlocked.Read(ref miLastMessageReceivedAtUtc)); }
            private set { Interlocked.Exchange(ref miLastMessageReceivedAtUtc, value.Ticks); }
        }

        public DateTime LastConnectionExceptionAtUtc
        {
            get { return new DateTime(Interlocked.Read(ref miLastConnectionExceptionAtUtc)); }
            private set { Interlocked.Exchange(ref miLastConnectionExceptionAtUtc, value.Ticks); }
        }

        private async Task Run()
        {
            CancellationToken oCT = moCTS.Token;
            try
            {
                Interlocked.Exchange(ref miMessageReadCount1, 0);
                Interlocked.Exchange(ref miMessageReadCount2, 0);
                await Connect();

                bool bRefreshRequired = false;
                while (!oCT.IsCancellationRequested)
                {
                    await Task.Delay(50);
                    int iMessageGapToleranceSeconds = DateTime.UtcNow < LastConnectionExceptionAtUtc.AddSeconds(60) ? 30 : 120;
                    bRefreshRequired = (LastMessageReceivedAtUtc.AddSeconds(iMessageGapToleranceSeconds) < DateTime.UtcNow);
                    if (bRefreshRequired)
                    {
                        Disconnect();
                        await Connect();
                        LastMessageReceivedAtUtc = DateTime.UtcNow;
                        bRefreshRequired = false;
                    }
                }
            }
            catch (Exception oException)
            {
                if (!oCT.IsCancellationRequested)
                {
                    moErrorQueue.Enqueue(new OpenRailFatalException("OpenRailNRODReceiver FAILED due to " +
                        OpenRailException.GetShortErrorInfo(oException), oException));

                    // rethrow the exception:
                    // this sets the Exception property of the Task object  
                    // i.e. makes this exception visible in the FatalException property of this class
                    throw oException;
                }
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task Connect()
        {
            DateTime dtAttemptToConnectUntilUtc = DateTime.UtcNow.AddSeconds(miAttemptToConnectForSeconds);
            DateTime dtNextConnectAtUtc = new DateTime(2000, 1, 1);
            int iDelayDurationMilliSeconds = 250;
            Exception oLastException = null;
            CancellationToken oCT = moCTS.Token;

            while (DateTime.UtcNow < dtAttemptToConnectUntilUtc)
            {
                if (dtNextConnectAtUtc < DateTime.UtcNow)
                {
                    if (TryConnect()) return;
                    // connect retry time doubles between each attempt (up to 1 minute) otherwise Open Data Service is overwhelmed during recovery
                    iDelayDurationMilliSeconds = Math.Min(iDelayDurationMilliSeconds * 2, 60000);
                    dtNextConnectAtUtc = DateTime.UtcNow.AddMilliseconds(iDelayDurationMilliSeconds);
                }
                await Task.Delay(500);
                if (oCT.IsCancellationRequested)
                    throw new OperationCanceledException("The connection attempt was cancelled due to OpenRailNRODReceiver.RequestStop() being called.");
            }

            if (oLastException == null)
                throw new OpenRailConnectTimeoutException("Timeout trying to connect to the message feed.");
            else
                throw new OpenRailConnectTimeoutException("Timeout trying to connect to the message feed.  The last connection error was: " +
                    oLastException.GetType().FullName + ": " + oLastException.Message, oLastException);
        }

        private bool TryConnect()
        {
            try
            {
                moConnectionFactory = new NMSConnectionFactory(new Uri(msConnectUrl));
                moConnection = moConnectionFactory.CreateConnection(msUser, msPassword);
                moConnection.ClientId = msUser;
                moConnection.ExceptionListener += new ExceptionListener(OnConnectionException);
                moSession = moConnection.CreateSession(AcknowledgementMode.AutoAcknowledge);
                if (!string.IsNullOrWhiteSpace(msTopic1))
                {
                    moTopic1 = moSession.GetTopic(msTopic1);
                    if (mbUseDurableSubscription) moConsumer1 = moSession.CreateDurableConsumer(moTopic1, msTopic1, null, false);
                    else moConsumer1 = moSession.CreateConsumer(moTopic1);
                    moConsumer1.Listener += new MessageListener(OnMessageReceived1);
                }
                if (!string.IsNullOrWhiteSpace(msTopic2))
                {
                    moTopic2 = moSession.GetTopic(msTopic2);
                    if (mbUseDurableSubscription) moConsumer2 = moSession.CreateDurableConsumer(moTopic2, msTopic2, null, false);
                    else moConsumer2 = moSession.CreateConsumer(moTopic2);
                    moConsumer2.Listener += new MessageListener(OnMessageReceived2);
                }

                LastMessageReceivedAtUtc = DateTime.UtcNow;
                SpinSpreadUntilUtc = DateTime.UtcNow.AddSeconds(30);

                moConnection.Start();
                Interlocked.Exchange(ref miIsConnected, 1);
                return true;
            }
            catch (Exception oException)
            {
                moErrorQueue.Enqueue(new OpenRailConnectException("Connection attempt failed: " +
                    OpenRailException.GetShortErrorInfo(oException), oException));
                Disconnect();
                return false;
            }
        }

        private void OnConnectionException(Exception exception)
        {
            try
            {
                OpenRailConnectionException oConnectionException = new OpenRailConnectionException(
                    OpenRailException.GetShortErrorInfo(exception), exception);
                moErrorQueue.Enqueue(oConnectionException);
                LastConnectionExceptionAtUtc = DateTime.UtcNow;
            }
            catch { }
        }

        private void OnMessageReceived1(IMessage message)
        {
            try
            {
                OpenRailMessage oMessage = null;

                // when the Apache code starts receiving messages, a number of worker threads are fired up (inside the Apache assembly)
                // these threads are all started up at close to the exact same time
                // this can lead to contention within the apache code, i.e.  blocking and slow throughput until the threads spread out
                // so, for the first half minute, inject some spin waits in the different worker threads to spread their activities out
                if (DateTime.UtcNow < SpinSpreadUntilUtc)
                {
                    long iSeed = Thread.CurrentThread.ManagedThreadId + (DateTime.Now.Ticks % Int32.MaxValue);
                    Thread.SpinWait(new Random((int)(iSeed % Int32.MaxValue)).Next(1, 1000000));
                }

                // text message
                ITextMessage msgText = message as ITextMessage;
                if (msgText != null) oMessage = new OpenRailTextMessage(msgText.NMSTimestamp, msgText.Text);

                // bytes message
                IBytesMessage msgBytes = message as IBytesMessage;
                if (msgBytes != null) oMessage = new OpenRailBytesMessage(message.NMSTimestamp, msgBytes.Content);

                // everything else
                if (oMessage == null) oMessage = new OpenRailUnsupportedMessage(message.NMSTimestamp, message.GetType().FullName);

                Interlocked.Increment(ref miMessageReadCount1);
                LastMessageReceivedAtUtc = DateTime.UtcNow;
                moMessageQueue1.Enqueue(oMessage);
            }
            catch (Exception oException)
            {
                moErrorQueue.Enqueue(new OpenRailMessageException("Message receive for topic 1 failed: " +
                    OpenRailException.GetShortErrorInfo(oException), oException));
            }
        }

        private void OnMessageReceived2(IMessage message)
        {
            try
            {
                OpenRailMessage oMessage = null;

                // when the Apache code starts receiving messages, a number of worker threads are fired up (inside the Apache assembly)
                // these threads are all started up at close to the exact same time
                // this can lead to contention within the apache code, i.e.  blocking and slow throughput until the threads spread out
                // so, for the first half minute, inject some spin waits in the different worker threads to spread their activities out
                if (DateTime.UtcNow < SpinSpreadUntilUtc)
                {
                    long iSeed = Thread.CurrentThread.ManagedThreadId + (DateTime.Now.Ticks % Int32.MaxValue);
                    Thread.SpinWait(new Random((int)(iSeed % Int32.MaxValue)).Next(1, 1000000));
                }

                // text message
                ITextMessage msgText = message as ITextMessage;
                if (msgText != null) oMessage = new OpenRailTextMessage(msgText.NMSTimestamp, msgText.Text);

                // bytes message
                IBytesMessage msgBytes = message as IBytesMessage;
                if (msgBytes != null) oMessage = new OpenRailBytesMessage(message.NMSTimestamp, msgBytes.Content);

                // everything else
                if (oMessage == null) oMessage = new OpenRailUnsupportedMessage(message.NMSTimestamp, message.GetType().FullName);

                Interlocked.Increment(ref miMessageReadCount2);
                LastMessageReceivedAtUtc = DateTime.UtcNow;
                moMessageQueue2.Enqueue(oMessage);
            }
            catch (Exception oException)
            {
                moErrorQueue.Enqueue(new OpenRailMessageException("Message receive for topic 2 failed: " +
                    OpenRailException.GetShortErrorInfo(oException), oException));
            }
        }

        private void Disconnect()
        {
            try
            {
                if (moConnection != null)
                {
                    try { if (moConnection != null) moConnection.Stop(); }
                    catch { }
                    try { if (moConsumer1 != null) moConsumer1.Close(); }
                    catch { }
                    try { if (moConsumer2 != null) moConsumer2.Close(); }
                    catch { }
                    try { if (moSession != null) moSession.Close(); }
                    catch { }
                    try { if (moConnection != null) moConnection.Close(); }
                    catch { }
                }
            }
            finally
            {
                moConnection = null;
                moConnectionFactory = null;
                moSession = null;
                moTopic1 = null;
                moTopic2 = null;
                moConsumer1 = null;
                moConsumer2 = null;
                Interlocked.Exchange(ref miIsConnected, 0);
            }
        }

        public void ListenForMessages()
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
                while ((moErrorQueue.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
                {
                    OpenRailException oOpenRailException = null;
                    if (moErrorQueue.TryDequeue(out oOpenRailException))
                    {
                        // the code here simply counts the errors, and captures the details of the last 
                        // error - your code may log details of errors to a database or log file
                        iErrorCount++;
                        msLastErrorInfo = OpenRailException.GetShortErrorInfo(oOpenRailException);
                    }
                }

                // attempt to dequeue and process some messages
                while ((moMessageQueue1.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
                {
                    OpenRailMessage oMessage = null;
                    if (moMessageQueue1.TryDequeue(out oMessage))
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
                while ((moMessageQueue2.Count > 0) && (DateTime.UtcNow < dtNextUiUpdateTime))
                {
                    OpenRailMessage oMessage = null;
                    if (moMessageQueue2.TryDequeue(out oMessage))
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
                    Console.WriteLine("Remaining Run Time = " + dtRunUntilUtc.Subtract(DateTime.UtcNow).TotalSeconds.ToString("###0.0") + " seconds");
                    Console.WriteLine();
                    Console.WriteLine("Receiver Status:");
                    Console.WriteLine("  Running = " + this.IsRunning.ToString() + ", Connected To Data Feed = " + this.IsConnected.ToString());
                    Console.WriteLine("  Size of local In-Memory Queue 1 (" + msTopic1 + ") = " + moMessageQueue1.Count.ToString()); // i.e. messages received from the feed but not yet processed locally
                    Console.WriteLine("  Size of local In-Memory Queue 2 (" + msTopic2 + ") = " + moMessageQueue2.Count.ToString()); // i.e. messages received from the feed but not yet processed locally
                    Console.WriteLine("  Last Message Received At = " + this.LastMessageReceivedAtUtc.ToLocalTime().ToString("HH:mm:ss.fff ddd dd MMM yyyy"));
                    Console.WriteLine("  Msg Counts:  (1: {0}) = {1}, (2: {2}) = {3}", msTopic1, this.MessageCount1, msTopic2, this.MessageCount2);
                    Console.WriteLine();
                    Console.WriteLine("Processing Status 1 (" + msTopic1 + "):");
                    Console.WriteLine("  Msg Counts: Text = {0}, Bytes = {1}, Unsupported = {2}", iTextMessageCount1, iBytesMessageCount1, iUnsupportedMessageCount1);
                    Console.WriteLine("  Last JSON = " + (msLastTextMessage1 == null ? "" : (msLastTextMessage1.Length > 40 ? msLastTextMessage1.Substring(0, 40) + "..." : msLastTextMessage1)));
                    Console.WriteLine();
                    Console.WriteLine("Processing Status 2 (" + msTopic2 + "):");
                    Console.WriteLine("  Msg Counts: Text = {0}, Bytes = {1}, Unsupported = {2}", iTextMessageCount2, iBytesMessageCount2, iUnsupportedMessageCount2);
                    Console.WriteLine("  Last JSON = " + (msLastTextMessage2 == null ? "" : (msLastTextMessage2.Length > 40 ? msLastTextMessage2.Substring(0, 40) + "..." : msLastTextMessage2)));
                    Console.WriteLine();
                    Console.WriteLine("Errors:  Total Errors = " + iErrorCount.ToString());
                    Console.WriteLine("  Last Error = " + (msLastErrorInfo == null ? "" : msLastErrorInfo));
                    Console.WriteLine();
                    dtNextUiUpdateTime = DateTime.UtcNow.AddMilliseconds(500);
                }

                if ((moMessageQueue1.Count < 10) && (moMessageQueue2.Count < 10)) Thread.Sleep(50);
            }

            Console.WriteLine("Stopping Receiver...");

            this.RequestStop();

            while (this.IsRunning)
            {
                Thread.Sleep(50);
            }

            Console.WriteLine("Receiver stopped.");
            Console.WriteLine("Finished.");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}