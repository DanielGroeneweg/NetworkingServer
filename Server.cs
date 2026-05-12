using NetworkConnections;
using OSCTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// The Server is the class that manages network connections with all clients, and 
/// communicates with the game code (Model classes).
/// </summary>
public class Server
{
    // ----- General server code:
    TcpListener listener;
    List<TcpNetworkConnection> connections;
    OSCDispatcher dispatcher;

    /// ------ TicTacToe Server code:
    TexasHoldemBoard board;
    Dictionary<TcpNetworkConnection, int> playerIDs = new Dictionary<TcpNetworkConnection, int>();

    TcpNetworkConnection host;

    bool gameStarted = false;

    public async Task Start()
    {
        // This server starts with a listener:
        int port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "50006");

        Logger.LogInfo("Starting server at " + port);
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        connections = new List<TcpNetworkConnection>();

        // Initialize the dispatcher and callbacks for incoming OSC messages:
        dispatcher = new OSCDispatcher();
        dispatcher.ShowIncomingMessages = true;
        Initialize();
        await Update();
    }

    async Task Update()
    {
        while (true)
        {
            AcceptNewConnections();
            UpdateConnections();
            CleanupConnections();
            await Task.Delay(10);
        }
    }

    void AcceptNewConnections()
    {
        if (listener.Pending())
        {
            TcpClient client = listener.AcceptTcpClient();
            TcpNetworkConnection connection = new TcpNetworkConnection(client);
            connections.Add(connection);
            Logger.LogInfo("Server: Adding new connection from " + connection.Remote);
            ClientJoined(connection);

            // Find the host

            if (connections.Count == 1)
            {
                host = connection;
                Logger.LogInfo("Server: Host is set to " + connection.Remote);
                host.Send(new OSCMessageOut("/SendHostInformation").GetBytes());
            }
        }
    }
    void ClientJoined(TcpNetworkConnection newClient)
    {
        if (playerIDs.Count < 6)
        {
            // We had fewer than 6 players, so this new client will be a player.
            int newID = playerIDs.Count + 1;
            playerIDs[newClient] = newID;
            Logger.LogInfo($"Registering new player: {newClient.Remote} = player {playerIDs[newClient]}");
            newClient.Send(new OSCMessageOut("/PlayerID").AddInt(newID).GetBytes());
        }
        else
        {
            Logger.LogInfo("Sorry - already have six players");
            // Note: this client is still allowed to join as spectator, but not as player!
            newClient.Send(new OSCMessageOut("/Spectator").GetBytes());
        }
    }

    void UpdateConnections()
    {
        foreach (TcpNetworkConnection conn in connections.ToList())
        {
            // The connection will call HandlePacket when a packet is available:
            while (conn.Available() > 0)
            {
                HandlePacket(conn.GetPacket(), conn.Remote);
            }
        }
    }

    void HandlePacket(byte[] packet, IPEndPoint remote)
    {
        OSCMessageIn mess = new OSCMessageIn(packet);
        Logger.LogInfo("Message arrives on server: " + mess);

        dispatcher.HandlePacket(packet, remote);
    }

    void CleanupConnections()
    {
        List<TcpNetworkConnection> dead = new();

        foreach (TcpNetworkConnection conn in connections)
        {
            if (!conn.IsConnected)
            {
                Logger.LogInfo("Client disconnected: " + conn.Remote);
                dead.Add(conn);
            }
        }

        foreach (TcpNetworkConnection conn in dead)
        {
            connections.Remove(conn);

            if (playerIDs.TryGetValue(conn, out int playerID))
            {
                playerIDs.Remove(conn);
                board.RemovePlayer(playerID);
                Logger.LogInfo($"Removed player {playerID}");
            }

            if (host == conn)
            {
                host = connections.Count > 0 ? connections[0] : null;
                if (host != null) host.Send(new OSCMessageOut("/SendHostInformation").GetBytes());
            }

            conn.Close();

            // Kick out the last player if everyone else disconnected, do Send a message to them though!
            if (gameStarted && playerIDs.Keys.Count <= 1)
            {
                Logger.LogInfo("No or 1 player(s) left! Re-Initializing server!");

                CleanUpServer();
                Initialize();
                return;
            }
        }
    }
    void CleanUpServer()
    {
        // Kick out all players/spectators
        foreach (TcpNetworkConnection connection in connections)
        {
            connection.Send(new OSCMessageOut("/KickPlayer").GetBytes());
            connection.Close();
        }
        connections.Clear();
        playerIDs.Clear();

        // Reset dispatcher
        dispatcher.RemoveListener("/Bet", BetRpc);
        dispatcher.RemoveListener("/Call", CallRpc);
        dispatcher.RemoveListener("/Check", CheckRpc);
        dispatcher.RemoveListener("/Raise", RaiseRpc);
        dispatcher.RemoveListener("/Fold", FoldRpc);
        dispatcher.RemoveListener("/NewRound", NewRoundRequestRpc);
        dispatcher.RemoveListener("/NewGame", NewGameRequestRpc);

        // Reset board events
        board.OnUpdatePot -= UpdatePotRpc;
        board.OnUpdatePlayerMoney -= UpdatePlayerMoneyRpc;
        board.OnNextPlayer -= NextPlayerRpc;
        board.OnChangePlayerOptions -= ChangePlayerRpc;
        board.OnNextPhase -= NextPhaseRpc;
        board.OnNewRound -= NewRoundRpc;
        board.OnDealPlayerCards -= DealPlayerCardsRpc;
        board.OnDealTableCards -= DealTableCardsRpc;
        board.OnInvalidAction -= InvalidActionRpc;
        board.OnInvalidNewRound -= InvalidNewRoundRpc;
        board.OnInvalidNewGame -= InvalidNewGameRpc;
        board.OnPlayerInformation -= PlayerInformationRpc;
        board.OnRoundEnd -= EndRoundRpc;
        board.OnGameEnd -= GameEndRpc;
        board.OnPlayerCardInfo -= PlayerCardInfoRpc;

        // Variable reset
        host = null;
        gameStarted = false;
    }
    void Initialize()
    {
        board = new TexasHoldemBoard();
        // Subscribe to game model events:
        // (Note: we try to keep the game code independent from networking details.)
        board.OnUpdatePot += UpdatePotRpc;
        board.OnUpdatePlayerMoney += UpdatePlayerMoneyRpc;
        board.OnNextPlayer += NextPlayerRpc;
        board.OnChangePlayerOptions += ChangePlayerRpc;
        board.OnNextPhase += NextPhaseRpc;
        board.OnNewRound += NewRoundRpc;
        board.OnDealPlayerCards += DealPlayerCardsRpc;
        board.OnDealTableCards += DealTableCardsRpc;
        board.OnInvalidAction += InvalidActionRpc;
        board.OnInvalidNewRound += InvalidNewRoundRpc;
        board.OnInvalidNewGame += InvalidNewGameRpc;
        board.OnPlayerInformation += PlayerInformationRpc;
        board.OnRoundEnd += EndRoundRpc;
        board.OnGameEnd += GameEndRpc;
        board.OnPlayerCardInfo += PlayerCardInfoRpc;

        //(Note: no unsubscribe needed in OnDestroy, since the server owns the private board variable.)

        // Subscribe listeners for incoming messages:
        // The (optional) list of parameter types (OSCUtil.INT) lets the dispatcher filter
        //  messages that do not satisfy the expected signature (=parameter list):
        dispatcher.AddListener("/Bet", BetRpc, OSCUtil.INT);
        dispatcher.AddListener("/Call", CallRpc);
        dispatcher.AddListener("/Check", CheckRpc);
        dispatcher.AddListener("/Raise", RaiseRpc, OSCUtil.INT);
        dispatcher.AddListener("/Fold", FoldRpc);
        dispatcher.AddListener("/NewRound", NewRoundRequestRpc);
        dispatcher.AddListener("/NewGame", NewGameRequestRpc, OSCUtil.INT);
    }

    // ----- Handle incoming RPCs (called by dispatcher):
    #region Incoming
    void BetRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int money = message.ReadInt();

        Logger.LogInfo($"S: bet ${money}. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            return;
        }
        // Looping over all players to find the player ID:
        //  a bit ugly, but acceptable since we only have two players.
        foreach (var conn in playerIDs.Keys)
        {
            Logger.LogInfo("Checking " + conn.Remote);
            // Warning: must use Equals, not == !
            // https://stackoverflow.com/questions/2782973/comparison-of-ipendpoint-objects-not-working !!!
            if (conn.Remote.Equals(remote))
            {
                Logger.LogInfo("This client is a player - allowed to make moves");
                board.Bet(playerIDs[conn], money);
            }
        }
    }
    void CheckRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Logger.LogInfo($"S: check. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            return;
        }
        // Looping over all players to find the player ID:
        //  a bit ugly, but acceptable since we only have two players.
        foreach (var conn in playerIDs.Keys)
        {
            Logger.LogInfo("Checking " + conn.Remote);
            // Warning: must use Equals, not == !
            // https://stackoverflow.com/questions/2782973/comparison-of-ipendpoint-objects-not-working !!!
            if (conn.Remote.Equals(remote))
            {
                Logger.LogInfo("This client is a player - allowed to make moves");
                board.Check(playerIDs[conn]);
            }
        }
    }
    void RaiseRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int money = message.ReadInt();

        Logger.LogInfo($"S: Raise ${money}. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            return;
        }
        // Looping over all players to find the player ID:
        //  a bit ugly, but acceptable since we only have two players.
        foreach (var conn in playerIDs.Keys)
        {
            Logger.LogInfo("Checking " + conn.Remote);
            // Warning: must use Equals, not == !
            // https://stackoverflow.com/questions/2782973/comparison-of-ipendpoint-objects-not-working !!!
            if (conn.Remote.Equals(remote))
            {
                Logger.LogInfo("This client is a player - allowed to make moves");
                board.Raise(playerIDs[conn], money);
            }
        }
    }
    void CallRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Logger.LogInfo($"S: call. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            return;
        }
        // Looping over all players to find the player ID:
        //  a bit ugly, but acceptable since we only have two players.
        foreach (var conn in playerIDs.Keys)
        {
            Logger.LogInfo("Checking " + conn.Remote);
            // Warning: must use Equals, not == !
            // https://stackoverflow.com/questions/2782973/comparison-of-ipendpoint-objects-not-working !!!
            if (conn.Remote.Equals(remote))
            {
                Logger.LogInfo("This client is a player - allowed to make moves");
                board.Call(playerIDs[conn]);
            }
        }
    }
    void FoldRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Logger.LogInfo($"S: fold. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            return;
        }
        // Looping over all players to find the player ID:
        //  a bit ugly, but acceptable since we only have two players.
        foreach (var conn in playerIDs.Keys)
        {
            Logger.LogInfo("Checking " + conn.Remote);
            // Warning: must use Equals, not == !
            // https://stackoverflow.com/questions/2782973/comparison-of-ipendpoint-objects-not-working !!!
            if (conn.Remote.Equals(remote))
            {
                Logger.LogInfo("This client is a player - allowed to make moves");
                board.Fold(playerIDs[conn]);
            }
        }
    }
    void NewRoundRequestRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int startingMoney = message.ReadInt();
        Logger.LogInfo($"S: new round. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            InvalidNewRoundRpc($"Need more players! currently {playerIDs.Count} player(s) in lobby");
            return;
        }
        // Check if message is sent by host
        if (remote.Equals(host.Remote))
        {
            Logger.LogInfo("S: Request sent by host");
            board.StartRound();
        }
    }
    void NewGameRequestRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Logger.LogInfo($"S: new game. Remote={remote}");
        if (playerIDs.Count < 2)
        {
            Logger.LogInfo("Waiting for more players");
            InvalidNewGameRpc($"Need more players! currently {playerIDs.Count} player(s) in lobby");
            return;
        }

        // Check if message is sent by host
        if (remote.Equals(host.Remote))
        {
            Logger.LogInfo("S: Request sent by host");
            board.StartGame(playerIDs.Count, message.ReadInt());
        }
    }
    #endregion

    // ----- Outgoing RPCs:
    #region Outgoing
    // These RPCs are called by game model events:
    void UpdatePotRpc(int pot)
    {
        OSCMessageOut message = new OSCMessageOut("/UpdatePot").AddInt(pot);
        Broadcast(message.GetBytes());
    }
    void UpdatePlayerMoneyRpc(int player, int playerMoney)
    {
        OSCMessageOut message = new OSCMessageOut("/UpdatePlayerMoney").AddInt(player).AddInt(playerMoney);
        Broadcast(message.GetBytes());
    }
    void NextPlayerRpc(int activePlayer, int actionTaken)
    {
        OSCMessageOut message = new OSCMessageOut("/NextPlayer").AddInt(activePlayer).AddInt(actionTaken);
        Broadcast(message.GetBytes());
    }
    void ChangePlayerRpc(int actionTaken, int player, int pot)
    {
        OSCMessageOut message = new OSCMessageOut("/ChangePlayer").AddInt(actionTaken).AddInt(pot);

        foreach (TcpNetworkConnection connection in playerIDs.Keys)
        {
            if (playerIDs[connection] == player)
            {
                connection.Send(message.GetBytes());
                break;
            }
        }
    }
    void NextPhaseRpc(int phase)
    {
        OSCMessageOut message = new OSCMessageOut("/NextPhase").AddInt(phase);
        Broadcast(message.GetBytes());
    }
    void NewRoundRpc()
    {
        OSCMessageOut message = new OSCMessageOut("/NewRound");
        Broadcast(message.GetBytes());
        gameStarted = true;
    }
    void DealPlayerCardsRpc(Card card1, Card card2, int player)
    {
        foreach (TcpNetworkConnection connection in playerIDs.Keys)
        {
            if (playerIDs[connection] == player)
            {
                OSCMessageOut message = new OSCMessageOut("/DealPlayerCards").AddInt((int)card1.rank).AddInt((int)card1.suit).AddInt((int)card2.rank).AddInt((int)card2.suit);
                connection.Send(message.GetBytes());
                break;
            }
        }
    }
    void DealTableCardsRpc(Card[] cards)
    {
        OSCMessageOut message = new OSCMessageOut("/DealTableCards");
        for (int i = 0; i < 5; i++)
        {
            Card card = cards[i];

            if (card == null)
                message.AddInt(-1).AddInt(-1);

            else
                message.AddInt((int)card.rank).AddInt((int)card.suit);
        }
        Broadcast(message.GetBytes());
    }
    void InvalidActionRpc(string error, int player)
    {
        OSCMessageOut message = new OSCMessageOut("/InvalidAction").AddString(error);
        foreach (TcpNetworkConnection connection in playerIDs.Keys)
        {
            if (playerIDs[connection] == player)
            {
                connection.Send(message.GetBytes());
                break;
            }
        }
    }
    void InvalidNewRoundRpc(string error)
    {
        OSCMessageOut message = new OSCMessageOut("/InvalidNewRound").AddString(error);
        host.Send(message.GetBytes());
    }
    void InvalidNewGameRpc(string error)
    {
        OSCMessageOut message = new OSCMessageOut("/InvalidNewGame").AddString(error);
        host.Send(message.GetBytes());
    }
    void PlayerInformationRpc(int playerAmount, int startingMoney)
    {
        OSCMessageOut message = new OSCMessageOut("/PlayerInformation").AddInt(playerAmount).AddInt(startingMoney);
        Broadcast(message.GetBytes());
    }
    void EndRoundRpc(List<int> winningPlayers)
    {
        bool[] winners = new bool[6];
        foreach (int player in winningPlayers) winners[player - 1] = true;

        OSCMessageOut message = new OSCMessageOut("/RoundEnd");
        foreach (bool winner in winners) message.AddBool(winner);
        Broadcast(message.GetBytes());
    }
    void GameEndRpc(int winner)
    {
        OSCMessageOut message = new OSCMessageOut("/GameEnd").AddInt(winner);
        Broadcast(message.GetBytes());
    }
    void Broadcast(byte[] packet)
    {
        foreach (var conn in connections)
        {
            conn.Send(packet);
        }
    }
    void PlayerCardInfoRpc(string data)
    {
        Logger.LogInfo("Sending card information now!");
        OSCMessageOut message = new OSCMessageOut("/PlayerCardInfo").AddString(data);
        Broadcast(message.GetBytes());
    }
    #endregion
}
