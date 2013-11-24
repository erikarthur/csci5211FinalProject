using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using socketSrv;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.IO;
using peer;

namespace ServerExperiment
{
    class Program
    {
        static public PeerToPeer p2p;
        
        static void Main(string[] args)
        {
            p2p = new PeerToPeer();
            p2p.connectCentralServer(args);
            p2p.runP2PNetwork();
        }
    }	

}
