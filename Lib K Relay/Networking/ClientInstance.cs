using Lib_K_Relay.Crypto;
using Lib_K_Relay.Networking.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Lib_K_Relay.Networking
{
    public class ClientInstance
    {
        public RC4 ClientReceiveKey = new RC4("311f80691451c71d09a13a2a6e");
        public RC4 ServerReceiveKey = new RC4("72c5583cafb6818995cdd74b80");
        public RC4 ClientSendKey = new RC4("72c5583cafb6818995cdd74b80");
        public RC4 ServerSendKey = new RC4("311f80691451c71d09a13a2a6e");

        private PacketBuffer _localBuffer = new PacketBuffer();
        private PacketBuffer _remoteBuffer = new PacketBuffer();

        private TcpClient _localConnection;
        private TcpClient _remoteConnection;
        private Proxy _proxy;

        public ClientInstance(Proxy proxy, TcpClient client)
        {
            _proxy = proxy;
            _localConnection = client;
            _remoteConnection = new TcpClient();

            _remoteConnection.BeginConnect(
                IPAddress.Parse(_proxy.RemoteAddress),
                _proxy.Port, RemoteConnected, null);
        }

        private void RemoteConnected(IAsyncResult ar)
        {
            _remoteConnection.EndConnect(ar);
            _proxy.FireClientConnected(this);

            BeginRemoteRead(0, 4); // Read 4 bytes (packet size)
            BeginLocalRead(0, 4); // Read 4 bytes (packet size)
        }

        private void RemoteReceive(IAsyncResult ar)
        {
            try
            {
                NetworkStream stream = _remoteConnection.GetStream();
                _remoteBuffer.Advance(stream.EndRead(ar));

                if (_remoteBuffer.Index() == 4)
                { // We have the first four bytes
                    // Resize the receive buffer.
                    _remoteBuffer.Resize(IPAddress.NetworkToHostOrder(
                        BitConverter.ToInt32(_remoteBuffer.Buffer(), 0)));

                    BeginRemoteRead(_remoteBuffer.Index(), _remoteBuffer.BytesRemaining());
                }
                else if (_remoteBuffer.BytesRemaining() > 0)
                { // Awaiting the rest of the packet
                    BeginRemoteRead(_remoteBuffer.Index(), _remoteBuffer.BytesRemaining());
                }
                else
                { // We have the full packet
                    ServerReceiveKey.Cipher(_remoteBuffer.Buffer());
                    Packet packet = new Packet(_remoteBuffer.Buffer(), true);

                    if (packet.Type != PacketType.UNKNOWN)
                        _proxy.FireServerPacket(this, packet);

                    if (packet.Send)
                        SendToClient(packet);

                    // Reset our counters and recieve a new one.
                    _remoteBuffer.Flush();
                    BeginRemoteRead(0, 4);
                }
            } catch (Exception e) { Close(e.Message); }
        }

        private void LocalReceive(IAsyncResult ar)
        {
            try
            {
                NetworkStream stream = _localConnection.GetStream();
                _localBuffer.Advance(stream.EndRead(ar));

                if (_localBuffer.Length() == 4)
                { // We have the first four bytes
                    // Resize the receive buffer
                    _localBuffer.Resize(IPAddress.NetworkToHostOrder(
                        BitConverter.ToInt32(_localBuffer.Buffer(), 0)));

                    BeginLocalRead(_localBuffer.Index(), _localBuffer.BytesRemaining());
                }
                else if (_localBuffer.BytesRemaining() > 0)
                { // Awaiting the rest of the packet
                    BeginLocalRead(_localBuffer.Index(), _localBuffer.BytesRemaining());
                }
                else
                { // We have the full packet
                    ClientReceiveKey.Cipher(_localBuffer.Buffer());
                    Packet packet = new Packet(_localBuffer.Buffer(), false);

                    if (packet.Type != PacketType.UNKNOWN)
                        _proxy.FireClientPacket(this, packet);

                    if (packet.Send)
                        SendToServer(packet);

                    // Reset our counters and recieve a new one
                    _localBuffer.Flush();
                    BeginLocalRead(0, 4);
                }
            } catch (Exception e) { Close(e.Message); }
        }

        private void BeginLocalRead(int offset, int amount)
        {
            _localConnection.GetStream().BeginRead(
                _localBuffer.Buffer(), offset, amount, LocalReceive, null);
        }

        private void BeginRemoteRead(int offset, int amount)
        {
            _remoteConnection.GetStream().BeginRead(
                _remoteBuffer.Buffer(), offset, amount, RemoteReceive, null);
        }

        public void RemoteConnect(string host, int port)
        {
            if (!_remoteConnection.Connected) _remoteConnection.Close();

            _remoteConnection = new TcpClient();
            _remoteConnection.BeginConnect(
                IPAddress.Parse(host),
                port, RemoteConnected, null);
        }

        public void SendToServer(Packet packet)
        {
            if (_remoteConnection == null) return;

            byte[] data = packet.Data();
            PacketWriter.BlockCopyInt32(data, data.Length);
            ServerSendKey.Cipher(data);

            NetworkStream remote = _remoteConnection.GetStream();
            remote.BeginWrite(data, 0, data.Length, (ar) => remote.EndWrite(ar), null);
        }

        public void SendToClient(Packet packet)
        {
            byte[] data = packet.Data();
            PacketWriter.BlockCopyInt32(data, data.Length);
            ClientSendKey.Cipher(data);

            NetworkStream local = _localConnection.GetStream();
            local.BeginWrite(data, 0, data.Length, (ar) => local.EndWrite(ar), null);
        }

        public void Close(string reason)
        {
            Console.WriteLine("[Connection] Client is disconnecting because {0}.", reason);
            if (!_localConnection.Connected) _localConnection.Close();
            if (!_remoteConnection.Connected) _remoteConnection.Close();

            _proxy.FireClientDisconnected(this);
        }
    }
}