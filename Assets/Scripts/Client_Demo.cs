using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Client_Demo : MonoBehaviour
{
    public ClientNetInfo m_ClientNetInfo = new ClientNetInfo();
    public enum ClientState { DISCONNECTED, CONNECTED }
    public ClientState m_State = ClientState.DISCONNECTED;

    public Peer peer { get; private set; }
    private NetworkReader m_NetworkReader;
    private NetworkWriter m_NetworkWriter;
    public static Client_Demo Instance;
    public Text Info;
    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Invoke("TestConnectLocalHost", 3);
    }

    public bool IsRunning
    {
        get
        {
            return peer != null;
        }
    }


    #region Connect/Disconnect
    public void Connect(string ip, int port, int retries, int retry_delay, int timeout)
    {
    CREATE_PEER:
        tmp_Banned = tmp_Fake = false;
        if (peer == null)
        {
            peer = Peer.CreateConnection(ip, port, retries, retry_delay, timeout);

            if (peer != null)
            {
                Debug.Log("[Client] Preparing to receiving...");
                m_NetworkReader = new NetworkReader(peer);
                m_NetworkWriter = new NetworkWriter(peer);
            }
        }
        else
        {
            peer.Close();
            peer = null;

            goto CREATE_PEER;
        }
    }

    public void Connect(string ip, int port)
    {
        Connect(ip, port, 30, 500, 30);
    }

    [ContextMenu("Test connect")]
    private void TestConnectLocalHost()
    {
        Connect("127.0.0.1", 27015);
    }

    public void Disconnect()
    {
        if (m_State == ClientState.CONNECTED)
        {
            OnDisconnected("");
            peer.Close();
            peer = null;
        }
    }
    #endregion

    private unsafe void FixedUpdate()
    {
        m_State = peer != null ? ClientState.CONNECTED : ClientState.DISCONNECTED;

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

            string ping_stat = "Ping: " + (
                peer != null &&
                m_ClientNetInfo != null &&
                m_ClientNetInfo.local_id != 0 ?
                peer.GetPingLast(m_ClientNetInfo.local_id) : 0) + " (avg: " + (m_ClientNetInfo.local_id != 0 ?peer.GetPingAverage(m_ClientNetInfo.local_id) : 0) + ")";

            Info.text = "Client Info:\n" + net_stat+"\n\n"+ping_stat+"\nLoss: "+peer.LOSS.ToString("f2")+"%";
        }
    }


    private bool tmp_Banned = false, tmp_Fake = false;


    /// <summary>
    /// Parsing packet
    /// </summary>
    /// <param name="packet_id">PACKET ID  - SEE Packets_ID.cs</param>
    private void OnReceivedPacket(byte packet_id)
    {
        bool IsInternalNetworkPackets = packet_id <= 134;

        if (IsInternalNetworkPackets)
        {
            if (packet_id == (byte)Peer.RakNet_Packets_ID.CONNECTION_REQUEST_ACCEPTED)
            {
                OnConnected(peer.incomingAddress);
            }

            if (packet_id == (byte)Peer.RakNet_Packets_ID.CONNECTION_ATTEMPT_FAILED)
            {
                OnDisconnected("Connection attempt failed");
            }

            if (packet_id == (byte)Peer.RakNet_Packets_ID.INCOMPATIBLE_PROTOCOL_VERSION)
            {
                OnDisconnected("Incompatible protocol version");
            }

            if (packet_id == (byte)Peer.RakNet_Packets_ID.CONNECTION_LOST)
            {
                OnDisconnected("Time out");
            }

            if (packet_id == (byte)Peer.RakNet_Packets_ID.NO_FREE_INCOMING_CONNECTIONS)
            {
                OnDisconnected("Server is full.");
            }

            if (packet_id == (byte)Peer.RakNet_Packets_ID.DISCONNECTION_NOTIFICATION && !tmp_Banned && !tmp_Fake)
            {
                OnDisconnected("You are kicked!");
            }
        }
        else
        {
            if (packet_id == (byte)Packets_ID.CL_INFO)
            {
                if (m_NetworkWriter.StartWritting())
                {
                    m_NetworkWriter.WritePacketID((byte)Packets_ID.CL_INFO);
                    m_NetworkWriter.Write(m_ClientNetInfo.name);
                    m_NetworkWriter.WritePackedUInt64(m_ClientNetInfo.local_id);
                    m_NetworkWriter.Write(m_ClientNetInfo.client_hwid);
                    m_NetworkWriter.Write(m_ClientNetInfo.client_version);
                    m_NetworkWriter.Send(peer.incomingGUID, Peer.Priority.Immediate, Peer.Reliability.Reliable, 0);//sending
                }
            }

            if (packet_id == (byte)Packets_ID.CL_ACCEPTED)
            {
                m_ClientNetInfo.net_id = m_NetworkReader.ReadPackedUInt64();

                Debug.Log("[Client] Accepted connection by server... [ID: " + m_ClientNetInfo.net_id + "]");
            }

            if (packet_id == (byte)Packets_ID.CL_BANNED)
            {
                tmp_Banned = true;
                OnDisconnected("You are banned on this server!");
            }

            if (packet_id == (byte)Packets_ID.CL_FAKE)
            {
                tmp_Fake = true;
                OnDisconnected("Fake client! Please wait few seconds...");
            }
        }
    }


    private void OnConnected(string address)
    {
        Debug.Log("[Client] Connected to " + address);

        //формируем/готовим информацию клиента
        m_ClientNetInfo.name = "Player_"+Environment.MachineName;
        m_ClientNetInfo.local_id = peer.incomingGUID;
        m_ClientNetInfo.client_hwid = SystemInfo.deviceUniqueIdentifier;
        m_ClientNetInfo.client_version = Application.version;
    }

    private void OnDisconnected(string reason)
    {
        Debug.LogError("[Client] Disconnected" + (reason.Length > 0 ? " with reason: " + reason : "..."));
    }
}
