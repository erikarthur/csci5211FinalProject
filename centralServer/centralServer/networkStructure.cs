using System;
using System.Net;

namespace CentralServer
{
	public class networkLinks
	{
		private IPAddress _server;
		private IPAddress _client;	
		
		public IPAddress server
		{
			get {return _server;}
			set {_server = value; }
		}
		
		public IPAddress client
		{
			get {return _client;}
			set {_client = value; }
		}
	}
}

