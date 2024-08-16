using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.HighPerformance;
using Hive.Both.General.Dispatchers;
using Hive.Network.Abstractions;
using Hive.Network.Abstractions.Session;
using Hive.Network.Shared;
using Hive.Network.Tcp;
using MiniGameRouter.SideCar.Interfaces;

namespace MiniGameRouter.SideCar;

public class SideCarServer : IHostedService
{
    private readonly IAcceptor<TcpSession> _acceptor;
    private readonly ConcurrentDictionary<SessionId, SessionId> _clientServerIdMappings = [];
    private readonly IConnector<TcpSession> _connector;
    private readonly IPEndPoint _destEndPoint;

    private readonly IDispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<SessionId, SessionId> _serverClientMappings = [];
    private readonly ConcurrentDictionary<SessionId, TcpSession> _sessionMappings = [];
    private readonly ISideCarOptionsProvider _sideCarOptionsProvider;

    public SideCarServer(
        IDispatcher dispatcher,
        IAcceptor<TcpSession> acceptor,
        IConnector<TcpSession> connector,
        ISideCarOptionsProvider sideCarOptionsProvider,
        ILogger<SideCarServer> logger)
    {
        _dispatcher = dispatcher;
        _acceptor = acceptor;
        _connector = connector;
        _sideCarOptionsProvider = sideCarOptionsProvider;
        _logger = logger;

        var destIp = IPAddress.Parse(sideCarOptionsProvider.Options.DestinationAddr);
        var destEndPoint = new IPEndPoint(destIp, sideCarOptionsProvider.Options.DestinationPort);

        _destEndPoint = destEndPoint;

        logger.LogInformation(
            "SideCar Server dest: [{address}:{port}]...",
            destEndPoint.Address,
            destEndPoint.Port);

        _acceptor.BindTo(_dispatcher);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listenAddr = IPAddress.Parse(_sideCarOptionsProvider.Options.Listen.Address);
        var listenEndPoint = new IPEndPoint(listenAddr, _sideCarOptionsProvider.Options.Listen.Port);

        await _acceptor.SetupAsync(listenEndPoint, cancellationToken);

        _acceptor.StartAcceptLoop(cancellationToken);
        _acceptor.OnSessionCreated += AcceptorOnSessionCreated;

        _logger.LogInformation(
            "SideCar server started on [{address}:{port}], waiting for in coming connections...",
            listenEndPoint.Address,
            listenEndPoint.Port);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _acceptor.TryCloseAsync(cancellationToken);
    }

    private async void AcceptorOnSessionCreated(IAcceptor acceptor, SessionId sessionId, TcpSession session)
    {
        _logger.LogInformation(
            "New incoming session created, id [{id}] from [{addr}:{port}]",
            sessionId,
            session.RemoteEndPoint!.Address,
            session.RemoteEndPoint.Port);

        using var cts =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(_sideCarOptionsProvider.Options.DestMaxConnectionTimeout));
        var serverSession = await _connector.ConnectAsync(_destEndPoint, cts.Token);

        if (serverSession == null)
        {
            _logger.LogError(
                "Failed to connect to dest [{addr}:{port}] within [{timeout}], now closing downstream...",
                _destEndPoint.Address,
                _destEndPoint.Port,
                _sideCarOptionsProvider.Options.DestMaxConnectionTimeout);

            session.Close();
            session.Dispose();

            return;
        }

        _logger.LogInformation("Connected to dest [{addr}:{port}] with session id [{id}]",
            _destEndPoint.Address,
            _destEndPoint.Port,
            serverSession.Id);

        _sessionMappings[sessionId] = session;
        _sessionMappings[serverSession.Id] = serverSession;
        _clientServerIdMappings[sessionId] = serverSession.Id;
        _serverClientMappings[serverSession.Id] = sessionId;

        session.OnSocketError += SessionOnOnSocketError;
        serverSession.OnSocketError += SessionOnOnSocketError;

        session.OnMessageReceived += SessionOnOnMessageReceived;
        serverSession.OnMessageReceived += SessionOnOnMessageReceived;
    }

    private void SessionOnOnSocketError(object? sender, SocketError e)
    {
        if (sender is not TcpSession session) return;

        session.OnMessageReceived -= SessionOnOnMessageReceived;
        session.OnSocketError -= SessionOnOnSocketError;

        _logger.LogCritical("Session [{id}] encountered socket error [{error}], closing...",
            session.Id,
            e);

        CleanupSession(_clientServerIdMappings, session.Id);
        CleanupSession(_serverClientMappings, session.Id);
    }

    private void CleanupSession(
        ConcurrentDictionary<SessionId, SessionId> mappings,
        SessionId id)
    {
        if (_serverClientMappings.TryRemove(id, out var sessionId) &&
            _sessionMappings.TryRemove(sessionId, out var session))
        {
            session.OnMessageReceived -= SessionOnOnMessageReceived;
            session.OnSocketError -= SessionOnOnSocketError;

            session.Close();
            session.Dispose();
        }
    }

    private async Task FetchMappingAndSend(
        ConcurrentDictionary<SessionId, SessionId> mappings,
        SessionId id,
        MemoryStream stream)
    {
        if (mappings.TryGetValue(id, out var sessionId) &&
            _sessionMappings.TryGetValue(sessionId, out var session))
        {
            await session.SendAsync(stream);

            _logger.LogInformation("Forwarded message from [{id}] to [{sessionId}] at [{addr}:{port}]",
                id,
                sessionId,
                session.RemoteEndPoint!.Address,
                session.RemoteEndPoint.Port);
        }
    }

    private async void SessionOnOnMessageReceived(ISession session, ReadOnlyMemory<byte> rawMessage)
    {
        var ms = RecycleMemoryStreamManagerHolder.Shared.GetStream();

        await rawMessage.AsStream().CopyToAsync(ms);
        await FetchMappingAndSend(_clientServerIdMappings, session.Id, ms);
        await FetchMappingAndSend(_serverClientMappings, session.Id, ms);
    }
}