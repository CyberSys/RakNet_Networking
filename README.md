# RakNet_Networking



This is a ready-made network solution for the Unity3D engine based on the cross-platform network engine RakNet that previously existed in Unity ...

The source code has a demo scene in which there are 2 scripts showing the network interaction between the client and the server.


# Examples


#### For send packets from a client or server, you first need to write data to a binary stream.


```php
              
                if (m_NetworkWriter.StartWritting())
                {
                    m_NetworkWriter.WritePacketID((byte)PACKET_ID);
                    m_NetworkWriter.Write("example string");
                    m_NetworkWriter.WritePackedUInt64(1234);
                    
                    //If you need to send from the server, you must specify the net_id connection from the Connection class
                    //If from the client to the server you need to specify local_id from the ClientNetInfo class
                    ulong guid = 0;
                    
                    m_NetworkWriter.Send(guid, Peer.Priority.Immediate, Peer.Reliability.Reliable, 0);
                }
```


#### For read data from a binary stream, use the sample code below.

```php
             {
                //Since the stream is cleared when the network stream is received, there is no need to use the
                //m_NetworkReader.StartReading () check, because, as shown in the demo version, the stream is 
                //cleared and then reads the first byte to identify the packet number, then 
                //the method is called OnReceivedPacket (byte) taking the packet number as an argument and 
                //there it is already processed by code using the m_NetworkReader class functional
                string s = m_NetworkReader.ReadString();//receiving text 'example string'
                ulong _integer64 = m_NetworkReader.ReadPacketUInt64();
             }
```


#### It is very important to use the sequence of writing to the stream, otherwise, if the data is not written sequentially, errors and exceptions will occur.
#### Suppose you have written a string to a stream and are trying to read a number from it


