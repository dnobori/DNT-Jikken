using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using Newtonsoft.Json;
using SoftEther.WebSocket;
using SoftEther.WebSocket.Helper;
using System.Security.Cryptography;

#pragma warning disable CS0162, CS1998, CS0169

namespace SoftEther.VpnClient
{
    public class RudpConsts
    {
        public const int NatTPort = 5004;
        public const int GetIPInterval = 5 * 1000;
        public const int GetIPIntervalMax = 150 * 1000;
        public const int GetIPIntervalAfter = 5 * 60 * 1000;

        public const string GetPrivateIPTcpServer = "www.msftncsi.com.";

        public const int PortForTCP1 = 80;
        public const int PortForTCP2 = 443;

        public const int Version = 1;

        public const int GetPrivateIPInterval = 15 * 60 * 1000;
        public const int GetPrivateIPIntervalAfterMin = 30 * 60 * 1000;
        public const int GetPrivateIPIntervalAfterMax = 60 * 60 * 1000;
        public const int GetPrivateIPConnectTimeout = 5 * 1000;

        public const int GetTokenInterval1 = 5 * 1000;
        public const int GetTokenIntervalFailMax = 20;
        public const int GetTokenInterval2Min = 20 * 60 * 1000;
        public const int GetTokenInterval2Max = 30 * 60 * 1000;

        public const int RegisterIntervalInitial = 5 * 1000;
        public const int RegisterIntervalFailMax = 20;
        public const int RegisterIntervalMin = 220 * 1000;
        public const int RegisterIntervalMax = 240 * 1000;

        public const int StatusCheckIntervalMin = 24 * 1000;
        public const int StatusCheckIntervalMax = 28 * 1000;

        public const int ConnectInterval = 200;

        public const int NatTIntervalMin = 5 * 60 * 1000;
        public const int NatTIntervalMax = 10 * 60 * 1000;
        public const int NatTIntervalInitial = 3 * 1000;
        public const int NatTIntervalFailMax = 60;
    }
}
