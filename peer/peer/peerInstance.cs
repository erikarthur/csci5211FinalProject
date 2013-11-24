using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;


namespace socketSrv
{
    [Serializable()]
    class peerInstance
    {
        private IPAddress _peerIP;
        private Int32 _peerPort;
        private SocketAsyncEventArgs _asyncSocketEvent;

        public SocketAsyncEventArgs asyncSocketEvent
        {
            get { return _asyncSocketEvent; }
            set { _asyncSocketEvent = value; }
        }
        public IPAddress peerIP
        {
            get { return _peerIP; }
            set { _peerIP = value; }
        }

        public Int32 peerPort
        {
            get { return _peerPort; }
            set { _peerPort = value; }
        }
    }
}
