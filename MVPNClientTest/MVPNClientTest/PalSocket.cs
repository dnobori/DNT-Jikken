using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using Newtonsoft.Json;
using SoftEther.WebSocket;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Diagnostics;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using System.Security.Authentication;

#pragma warning disable CS0162

namespace SoftEther.WebSocket.Helper
{
    class PalSocket : IDisposable
    {
        Socket _Socket;

        public AddressFamily AddressFamily { get; }
        public SocketType SocketType { get; }
        public ProtocolType ProtocolType { get; }

        object LockObj = new object();

        public PalSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;

            _Socket = new Socket(addressFamily, socketType, protocolType);
        }

        public bool NoDelay { get => _Socket.NoDelay; set => _Socket.NoDelay = value; }

        bool _NoLinger = false;
        public bool NoLinger
        {
            get => _NoLinger;

            set
            {
                lock (LockObj)
                {
                    if (value == _NoLinger) return;
                    _Socket.NoDelay = value;
                    try
                    {
                        _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, value);
                    }
                    catch { }
                    _NoLinger = value;
                }
            }
        }

        public Task<int> SendAsync(IEnumerable<Memory<byte>> buffers)
        {
            List<ArraySegment<byte>> sendArraySegmentsList = new List<ArraySegment<byte>>();
            foreach (Memory<byte> mem in buffers)
                sendArraySegmentsList.Add(mem.AsSegment());

            return _Socket.SendAsync(sendArraySegmentsList, SocketFlags.None);
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                _Socket.DisposeSafe();
            }
        }
    }
}
