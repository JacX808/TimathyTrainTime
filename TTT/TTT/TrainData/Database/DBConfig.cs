namespace TTT.TrainData.Database;

public sealed class DbConfig(string host, int port, string databaseName, string userName, string password)
{
    internal string Host { get; } = host;
    internal int Port { get; } = port;
    internal string DatabaseName { get; } = databaseName;
    internal string UserName { get; } = userName;
    internal string Password { get; } = password;
}