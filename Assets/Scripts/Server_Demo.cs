using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Peer;
using UnityEngine.UI;
public class Server_Demo : MonoBehaviour
{
    public Peer peer { get; private set; }
    private NetworkReader m_NetworkReader;
    private NetworkWriter m_NetworkWriter;

    public static Server_Demo Instance;
    public Text Info;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartServer("127.0.0.1", 27015, 500);//starting server...
    }

    public void StopServer()
    {
        if (peer != null)
        {
            peer.Close();
            Debug.LogError("[Server] Shutting down...");
        }
    }

    private void OnDestroy()
    {
        StopServer();
    }

    public int MaxConnections { get; private set; } = -1;

    public bool StartServer(string ip, int port, int maxConnections)
    {
        if (peer == null)
        {
            peer = new Peer();
            peer = CreateServer(ip, port, maxConnections);

            if (peer != null)
            {
                MaxConnections = maxConnections;
                Debug.Log("[Server] Server initialized on port " + port);

                Debug.Log("-------------------------------------------------");
                Debug.Log("|     Max connections: " + maxConnections);
                Debug.Log("|     Max FPS: " + (Application.targetFrameRate != -1 ? Application.targetFrameRate : 1000) + "(" + Time.deltaTime.ToString("f3") + " ms)");
                Debug.Log("|     Tickrate: " + (1 / Time.fixedDeltaTime) + "(" + Time.fixedDeltaTime.ToString("f3") + " ms)");
                Debug.Log("-------------------------------------------------");

                m_NetworkReader = new NetworkReader(peer);
                m_NetworkWriter = new NetworkWriter(peer);

                return true;
            }
            else
            {
                Debug.LogError("[Server] Starting failed...");

                return false;
            }
        }
        else
        {
            return true;
        }
    }




    private void FixedUpdate()
    {
        if (peer != null)
        {
            while (peer.Receive())
            {
                m_NetworkReader.StartReading();
                byte b = m_NetworkReader.ReadByte();

                OnReceivedPacket(b);
            }


            string net_stat = peer != null ?
                    string.Format("in: {0} kb\t\t\t out: {1} kb\nin: {2} k/s\t\t\t out: {3} k/s",
                    ((double)peer.TOTAL_RECEIVED_BYTES / 1024).ToString("f2"),
                    ((double)peer.TOTAL_SENDED_BYTES / 1024).ToString("f2"),
                    ((double)peer.BYTES_IN / 1024).ToString("f2"),
                    ((double)peer.BYTES_OUT / 1024).ToString("f2")) : "-/-";

            Info.text = "Server Info:\n" + net_stat + "\nLoss: " + peer.LOSS.ToString("f2") + "%"+"\n\nConnections: "+connections.Count+"/"+MaxConnections;
        }
    }


    private void OnReceivedPacket(byte packet_id)
    {
        bool IsInternalNetworkPackets = packet_id <= 134;

        if (IsInternalNetworkPackets)
        {
            if (packet_id == (byte)RakNet_Packets_ID.NEW_INCOMING_CONNECTION)
            {
                OnConnected();//добавляем соединение
            }

            if (packet_id == (byte)RakNet_Packets_ID.CONNECTION_LOST || packet_id == (byte)RakNet_Packets_ID.DISCONNECTION_NOTIFICATION)
            {
                Connection conn = FindConnection(peer.incomingGUID);

                if (conn != null)
                {
                    OnDisconnected(FindConnection(peer.incomingGUID));//удаляем соединение
                }
            }
        }
        else
        {
            if (packet_id == (byte)Packets_ID.CL_INFO)
            {
                OnReceivedClientNetInfo(peer.incomingGUID);
            }
        }
    }



    #region Connections
    public List<Connection> connections = new List<Connection>();
    private Dictionary<ulong, Connection> connectionByGUID = new Dictionary<ulong, Connection>();

    public List<ulong> guids = new List<ulong>();

    public Connection FindConnection(ulong guid)
    {
        if (connectionByGUID.TryGetValue(guid, out Connection value))
        {
            return value;
        }
        return null;
    }

    private void AddConnection(Connection connection)
    {
        connections.Add(connection);
        connectionByGUID.Add(connection.guid, connection);
        guids.Add(connection.guid);
    }

    private void RemoveConnection(Connection connection)
    {
        connectionByGUID.Remove(connection.guid);
        connections.Remove(connection);
        guids.Remove(connection.guid);
    }

    public static Connection[] Connections
    {
        get
        {
            return Instance.connections.ToArray();
        }
    }

    public static Connection GetByID(int id)
    {
        if (Connections.Length > 0)
        {
            return Connections[id];
        }

        return null;
    }

    public static Connection GetByIP(string ip)
    {
        foreach (Connection c in Connections)
        {
            if (c.ipaddress == ip)
            {
                return c;
            }
        }

        return null;
    }

    public static Connection GetByName(string name)
    {
        foreach (Connection c in Connections)
        {
            if (c.Info.name == name)
            {
                return c;
            }
        }

        return null;
    }

    public static Connection GetByHWID(string hwid)
    {
        foreach (Connection c in Connections)
        {
            if (c.Info.client_hwid == hwid)
            {
                return c;
            }
        }

        return null;
    }

    #endregion

    #region Events
    private void OnConnected()
    {
        Connection connection = new Connection(peer, peer.incomingGUID, connections.Count);

        //добавляем в список соединений
        AddConnection(connection);

        Debug.Log("[Server] Connection established " + connection.ipaddress);

        peer.SendPacket(connection, Packets_ID.CL_INFO, m_NetworkWriter);
    }

    private void OnDisconnected(Connection connection)
    {
        if (connection != null)
        {
            try
            {
                Debug.LogError("[Server] " + connection.Info.name + " disconnected [IP: " + connection.ipaddress + "]");

                RemoveConnection(connection);
            }
            catch
            {
                Debug.LogError("[Server] Unassgigned connection destroyed!");
            }
        }
    }

    private void OnReceivedClientNetInfo(ulong guid)
    {
        Connection connection = FindConnection(guid);

        if (connection != null)
        {
            if (connection.Info == null)
            {
                connection.Info = new ClientNetInfo();
                connection.Info.net_id = guid;
                connection.Info.name = m_NetworkReader.ReadString();
                connection.Info.local_id = m_NetworkReader.ReadPackedUInt64();
                connection.Info.client_hwid = m_NetworkReader.ReadString();
                connection.Info.client_version = m_NetworkReader.ReadString();
            }
            else
            {
                peer.SendPacket(connection, Packets_ID.CL_FAKE, Reliability.Reliable, m_NetworkWriter);
                peer.Kick(connection, 1);
            }
        }
    }
    #endregion


    public InputField Guid;

    public void OnKickClicked()
    {
        Connection connection = FindConnection(ulong.Parse(Guid.text));

        if (connection != null)
        {
            peer.Kick(connection);
        }
    }

    public void OnBanClicked()
    {
        Connection connection = FindConnection(ulong.Parse(Guid.text));

        if (connection != null)
        {
            peer.SendPacket(connection, Packets_ID.CL_BANNED, Reliability.Reliable, m_NetworkWriter);
            peer.Kick(connection, 1);
        }
    }
}