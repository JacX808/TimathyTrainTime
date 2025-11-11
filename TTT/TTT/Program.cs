using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TTT.Database;
using TTT.OpenRail;
using TTT.TrainData.Connection;
using TTT.TrainData.DataSets;


internal class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // DB Connection
        string host = builder.Configuration["DB_HOST"] ?? "localhost";
        string port = builder.Configuration["DB_PORT"] ?? "5432";
        string user = builder.Configuration["DB_USERNAME"] ?? "app";
        string pass = builder.Configuration["DB_PASSWORD"] ?? "app";
        string db = builder.Configuration["DB_NAME"] ?? "ttt";

        var conn = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";
        builder.Services.AddDbContext<TttDbContext>(o => o.UseNpgsql(conn));

        // Swagger & controllers
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // In-memory cache
        builder.Services.AddSingleton<LiveMovementCache>();

        // Open RailOptions + background consumer
        string sConnectUrl = "activemq:tcp://publicdatafeeds.networkrail.co.uk:61619?transport.useInactivityMonitor=false&initialReconnectDelay=250&reconnectDelay=500&consumerExpiryCheckEnabled=false";
        string sUser = builder.Configuration["OpenRail:NR_USERNAME"] ?? "***";
        string sPassword =  builder.Configuration["OpenRail:NR_PASSWORD"] ??"***";
        string sTopic1 = "TRAIN_MVT_ALL_TOC";
        string sTopic2 = "VSTP_ALL";
        bool bUseDurableSubscription = true;
        
        if (sUser == "***" || sPassword == "***")
        {
            Console.WriteLine("Connection to OpenRail failed: ");
            Console.WriteLine();
            Console.WriteLine("ERROR:  Username and password did not match.");
            Console.ReadLine();
            return;
        }
        
        // create the shared queues (into which the receiver will enqueue messages/errors)
        ConcurrentQueue<OpenRailMessage> oMessageQueue1 = new ConcurrentQueue<OpenRailMessage>();
        ConcurrentQueue<OpenRailMessage> oMessageQueue2 = new ConcurrentQueue<OpenRailMessage>();
        ConcurrentQueue<OpenRailException> oErrorQueue = new ConcurrentQueue<OpenRailException>();

        
        // create the receiver
        OpenRailNRODReceiver oNrodReceiver = new OpenRailNRODReceiver(
            sConnectUrl, sUser, sPassword, sTopic1, sTopic2, oMessageQueue1, oMessageQueue2, oErrorQueue, bUseDurableSubscription, 100);
    
        // Start the receiver
        oNrodReceiver.Start();
        
        builder.Services.Configure<OpenRailOptions>(builder.Configuration.GetSection("OpenRail"));
        //builder.Services.AddHostedService < TrainMovementsConsumer);

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        app.Run();
        
        oNrodReceiver.ListenForMessages();
    }
}