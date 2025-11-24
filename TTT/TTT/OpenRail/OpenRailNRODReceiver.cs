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
using Microsoft.Extensions.Options;
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
        private IConnectionFactory? _connectionFactory;
        private IConnection? _connection;
        private ISession? _session;
        private ITopic? _topic1;
        private ITopic? _topic2;
        private IMessageConsumer? _consumer1;
        private IMessageConsumer? _consumer2;

        private readonly ILogger<OpenRailNRODReceiver> _log;

        private readonly string _msConnectUrl;
        private readonly string _msUser;
        private readonly string _msPassword;
        public readonly string MsTopic1;
        public readonly string MsTopic2;
        private readonly bool _mbUseDurableSubscription;
        private readonly int _miAttemptToConnectForSeconds;

        public readonly ConcurrentQueue<OpenRailMessage> MoMessageQueue1 = new ConcurrentQueue<OpenRailMessage>();
        public readonly ConcurrentQueue<OpenRailMessage> MoMessageQueue2 = new ConcurrentQueue<OpenRailMessage>();
        public readonly ConcurrentQueue<OpenRailException> MoErrorQueue = new ConcurrentQueue<OpenRailException>();

        private CancellationTokenSource _moCts;
        private Task _mtManagement;

        private long _miSpinSpreadUntilUtc = (new DateTime(2000, 1, 1)).Ticks;
        private long _miIsConnected = 0;
        private long _miLastMessageReceivedAtUtc = (new DateTime(2000, 1, 1)).Ticks;
        private long _miMessageReadCount1 = 0;
        private long _miMessageReadCount2 = 0;
        private long _miLastConnectionExceptionAtUtc = (new DateTime(2000, 1, 1)).Ticks;

        // TODO: Cleanup
        public OpenRailNRODReceiver(IOptions<NetRailOptions> opts, ILogger<OpenRailNRODReceiver> log)
        {
            var opts1 = opts.Value;
            _log  = log;
            _miAttemptToConnectForSeconds = 200;

            _msConnectUrl = opts1.ConnectUrl;
            _msUser = opts1.Username ?? "***";
            _msPassword = opts1.Password ?? "***";
            MsTopic1 = opts1.Topics.ElementAtOrDefault(0) ?? "TRAIN_MVT_ALL_TOC";
            MsTopic2 = opts1.Topics.ElementAtOrDefault(1) ?? "VSTP_ALL";
            _mbUseDurableSubscription = opts1.UseDurableSubscription;

            Start();
        }

        private void Start()
        {
            lock (this)
            {
                _moCts = new CancellationTokenSource();
                _mtManagement = Task.Run((Func<Task>)Run);
                _mtManagement.ConfigureAwait(false);
            }
        }

        public bool IsRunning => !(_mtManagement.IsCanceled || _mtManagement.IsCompleted || _mtManagement.IsFaulted);

        public bool IsConnected => Interlocked.Read(ref _miIsConnected) > 0;

        public long MessageCount1 => Interlocked.Read(ref _miMessageReadCount1);

        public long MessageCount2 => Interlocked.Read(ref _miMessageReadCount2);

        public Exception? FatalException => _mtManagement.IsFaulted ? _mtManagement.Exception : null;

        public void RequestStop()
        {
            _moCts.Cancel();
        }

        private DateTime SpinSpreadUntilUtc
        {
            get => new(Interlocked.Read(ref _miSpinSpreadUntilUtc));
            set => Interlocked.Exchange(ref _miSpinSpreadUntilUtc, value.Ticks);
        }

        public DateTime LastMessageReceivedAtUtc
        {
            get => new(Interlocked.Read(ref _miLastMessageReceivedAtUtc));
            private set { Interlocked.Exchange(ref _miLastMessageReceivedAtUtc, value.Ticks); }
        }

        private DateTime LastConnectionExceptionAtUtc
        {
            get => new(Interlocked.Read(ref _miLastConnectionExceptionAtUtc));
            set => Interlocked.Exchange(ref _miLastConnectionExceptionAtUtc, value.Ticks);
        }

        private async Task Run()
        {
            CancellationToken oCT = _moCts.Token;
            try
            {
                Interlocked.Exchange(ref _miMessageReadCount1, 0);
                Interlocked.Exchange(ref _miMessageReadCount2, 0);
                await Connect();

                bool bRefreshRequired = false;
                while (!oCT.IsCancellationRequested)
                {
                    await Task.Delay(50);
                    int iMessageGapToleranceSeconds =
                        DateTime.UtcNow < LastConnectionExceptionAtUtc.AddSeconds(60) ? 30 : 120;
                    bRefreshRequired = (LastMessageReceivedAtUtc.AddSeconds(iMessageGapToleranceSeconds) <
                                        DateTime.UtcNow);
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
                    MoErrorQueue.Enqueue(new OpenRailFatalException($"OpenRailNRODReceiver FAILED due to {OpenRailException.GetShortErrorInfo(oException)}" +
                        $"{oException.StackTrace}",
                        oException));

                    // rethrow the exception:
                    // this sets the Exception property of the Task object  
                    // i.e. makes this exception visible in the FatalException property of this class
                    throw;
                }
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task Connect()
        {
            DateTime dtAttemptToConnectUntilUtc = DateTime.UtcNow.AddSeconds(_miAttemptToConnectForSeconds);
            DateTime dtNextConnectAtUtc = new DateTime(2000, 1, 1);
            int iDelayDurationMilliSeconds = 250;
            Exception oLastException = null;
            CancellationToken oCT = _moCts.Token;

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
                    throw new OperationCanceledException(
                        "The connection attempt was cancelled due to OpenRailNRODReceiver.RequestStop() being called.");
            }

            if (oLastException == null)
                throw new OpenRailConnectTimeoutException("Timeout trying to connect to the message feed.");
            else
                throw new OpenRailConnectTimeoutException(
                    "Timeout trying to connect to the message feed.  The last connection error was: " +
                    oLastException.GetType().FullName + ": " + oLastException.Message, oLastException);
        }

        private bool TryConnect()
        {
            try
            {
                _connectionFactory = new NMSConnectionFactory(_msConnectUrl);
                _connection = _connectionFactory.CreateConnection(_msUser, _msPassword);
                _connection.ClientId = _msUser;
                _connection.ExceptionListener += new ExceptionListener(OnConnectionException);
                _session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
                if (!string.IsNullOrWhiteSpace(MsTopic1))
                {
                    _topic1 = _session.GetTopic(MsTopic1);
                    if (_mbUseDurableSubscription)
                        _consumer1 = _session.CreateDurableConsumer(_topic1, MsTopic1, null, false);
                    else _consumer1 = _session.CreateConsumer(_topic1);
                    _consumer1.Listener += new MessageListener(OnMessageReceived1);
                }

                if (!string.IsNullOrWhiteSpace(MsTopic2))
                {
                    _topic2 = _session.GetTopic(MsTopic2);
                    if (_mbUseDurableSubscription)
                        _consumer2 = _session.CreateDurableConsumer(_topic2, MsTopic2, null, false);
                    else _consumer2 = _session.CreateConsumer(_topic2);
                    _consumer2.Listener += new MessageListener(OnMessageReceived2);
                }

                LastMessageReceivedAtUtc = DateTime.UtcNow;
                SpinSpreadUntilUtc = DateTime.UtcNow.AddSeconds(30);

                _connection.Start();
                Interlocked.Exchange(ref _miIsConnected, 1);
                return true;
            }
            catch (Exception oException)
            {
                MoErrorQueue.Enqueue(new OpenRailConnectException("Connection attempt failed: " +
                                                                  OpenRailException.GetShortErrorInfo(oException),
                    oException));
                Disconnect();
                _log.LogError($"Error: Connection failed {OpenRailException.GetShortErrorInfo(oException)}");
                return false;
            }
        }

        private void OnConnectionException(Exception exception)
        {
            try
            {
                OpenRailConnectionException oConnectionException = new OpenRailConnectionException(
                    OpenRailException.GetShortErrorInfo(exception), exception);
                MoErrorQueue.Enqueue(oConnectionException);
                LastConnectionExceptionAtUtc = DateTime.UtcNow;
            }
            catch
            {
            }
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

                switch (message)
                {
                    // text message
                    case ITextMessage msgText:
                        oMessage = new OpenRailTextMessage(msgText.NMSTimestamp, msgText.Text);
                        break;
                    // bytes message
                    case IBytesMessage msgBytes:
                        oMessage = new OpenRailBytesMessage(message.NMSTimestamp, msgBytes.Content);
                        break;
                }

                // everything else
                var sMessageType = message.GetType().FullName;
                if (sMessageType != null)
                    oMessage ??= new OpenRailUnsupportedMessage(message.NMSTimestamp, sMessageType);

                Interlocked.Increment(ref _miMessageReadCount1);
                LastMessageReceivedAtUtc = DateTime.UtcNow;
                
                if (oMessage != null)
                    MoMessageQueue1.Enqueue(oMessage);
            }
            catch (Exception oException)
            {
                MoErrorQueue.Enqueue(new OpenRailMessageException("Message receive for topic 1 failed: " +
                                                                  OpenRailException.GetShortErrorInfo(oException),
                    oException));
            }
        }

        private void OnMessageReceived2(IMessage message)
        {
            try
            {
                OpenRailMessage oMessage = null!;

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
                ITextMessage? msgText = message as ITextMessage;
                if (msgText != null) oMessage = new OpenRailTextMessage(msgText.NMSTimestamp, msgText.Text);

                // bytes message
                IBytesMessage? msgBytes = message as IBytesMessage;
                if (msgBytes != null) oMessage = new OpenRailBytesMessage(message.NMSTimestamp, msgBytes.Content);

                // everything else
                if (oMessage == null)
                    oMessage = new OpenRailUnsupportedMessage(message.NMSTimestamp, message.GetType().FullName);

                Interlocked.Increment(ref _miMessageReadCount2);
                LastMessageReceivedAtUtc = DateTime.UtcNow;
                MoMessageQueue2.Enqueue(oMessage);
            }
            catch (Exception oException)
            {
                MoErrorQueue.Enqueue(new OpenRailMessageException("Message receive for topic 2 failed: " +
                                                                  OpenRailException.GetShortErrorInfo(oException),
                    oException));
            }
        }

        // TODO: Cleanup
        private void Disconnect()
        {
            try
            {
                try
                {
                    _connection?.Stop();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    _consumer1?.Close();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    _consumer2?.Close();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    _session?.Close();
                }
                catch
                {
                    // ignored
                }

                try
                {
                    _connection?.Close();
                }
                catch
                {
                    // ignored
                }
            }
            finally
            {
                _connection = null;
                _connectionFactory = null;
                _session = null;
                _topic1 = null;
                _topic2 = null;
                _consumer1 = null;
                _consumer2 = null;
                Interlocked.Exchange(ref _miIsConnected, 0);
            }
        }
    }
}