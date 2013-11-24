using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace socketSrv
{
    class networkList
    {
        private IPAddress _machineIP;
        private string _machineName;
        private Int32 _machinePort;

        public IPAddress machineIP
        {
            get {return _machineIP;}
            set {_machineIP = value;}
        }

        public string machineName
        {
            get { return _machineName; }
            set { _machineName = value; }
        }
        public Int32 machinePort
        {
            get { return _machinePort; }
            set { _machinePort = value; }
        }

        public networkList(IPAddress ip, string name, Int32 port)
        {
            this.machineIP = ip;
            this.machineName = name;
            this.machinePort = port;
        }
        public networkList()
        {
            this.machineIP = IPAddress.Parse("0.0.0.0");
            this.machineName = "";
            this.machinePort = 0;
        }

    }
}
