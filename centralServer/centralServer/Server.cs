
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;


namespace CentralServer
{
  
    class Server : IDisposable
    {
        private int numConnections;                     // the maximum number of connections the sample is designed to handle simultaneously 
        private int receiveBufferSize;                  // buffer size to use for each socket I/O operation 
        BufferManager bufferManager;                    // represents a large reusable set of buffers for all socket operations
        const int opsToPreAlloc = 2;                    // read, write (don't alloc buffer space for accepts)
        Socket listenSocket;                            // the socket used to listen for incoming connection requests
        SocketAsyncEventArgsStack asyncSocketStack;     // stack of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
        int numConnectedSockets;                        // the total number of clients connected to the server 
        Semaphore maxNumberAcceptedClients;
        public List<peerInstance> peerList;
		public List<networkLinks> peerConnections = new List<networkLinks>();
		List<IPEndPoint> ipInUse = new List<IPEndPoint>();
		public List<SocketAsyncEventArgs> myAsyncList = new List<SocketAsyncEventArgs>();

        public Server(int numConns, int receiveSize)
        {
            numConnectedSockets = 0;
            numConnections = numConns;
            receiveBufferSize = receiveSize;
            bufferManager = new BufferManager(receiveBufferSize * numConnections * opsToPreAlloc, receiveBufferSize, numConnections);
            asyncSocketStack = new SocketAsyncEventArgsStack(numConnections);
            maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
            peerList = new List<peerInstance>();
        }

        public void Dispose()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }

        public void Init()
        {
            // Allocates one large byte buffer which all I/O operations use a piece of.  This gaurds 
            // against memory fragmentation
             bufferManager.initBuffers();

            // preallocate pool of SocketAsyncEventArgs objects
            SocketAsyncEventArgs socketEventArg;

            for (int i = 0; i < numConnections; i++)
            {
                //Pre-allocate a set of reusable SocketAsyncEventArgs
                socketEventArg = new SocketAsyncEventArgs();
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(SendRecieve_Completed);
                socketEventArg.UserToken = new AsyncUserToken();
                socketEventArg.SendPacketsSendSize = 1500;

                // assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
                bufferManager.assignBuffer(socketEventArg);

                // add SocketAsyncEventArg to the pool
                asyncSocketStack.Push(socketEventArg);
            }

        }

        public void Start(IPEndPoint localEndPoint)
        {
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            
            listenSocket.Listen(20);

            StartAccept(null);

            Console.WriteLine("Press any key to terminate the server process....");
            Console.ReadKey();
        }


        public void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            }
            else
            {
                // socket must be cleared since the context object is being reused
                acceptEventArg.AcceptSocket = null;
            }

            maxNumberAcceptedClients.WaitOne();

            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref numConnectedSockets);
            Console.WriteLine("Client connection accepted. There are {0} clients connected to the server",
                numConnectedSockets);

            SocketAsyncEventArgs socketEventArgs = asyncSocketStack.Pop();
            ((AsyncUserToken)socketEventArgs.UserToken).Socket = e.AcceptSocket;
  			myAsyncList.Add(socketEventArgs);

            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(socketEventArgs);

            IPEndPoint iep = (IPEndPoint)e.AcceptSocket.RemoteEndPoint;
			ipInUse.Add(iep);
            //Console.WriteLine("New Peer is {0}", iep.Address);

            if (!willRaiseEvent)
            {
                ProcessReceive(socketEventArgs);
            }

            // Accept the next connection request
            StartAccept(e);
        }

        void SendRecieve_Completed(object sender, SocketAsyncEventArgs e)
        {
            //if (e.RemoteEndPoint != null)
            //{
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        ProcessReceive(e);
                        break;
                    case SocketAsyncOperation.Send:
                        ProcessSend(e);
                        break;
                    default:
                        throw new ArgumentException("The last operation completed on the socket was not a receive or send");
                }
            //}

        }

        //member function for parsing command messages.
        private commandMessage parseCommandMessage(byte[] buf, int bufBytes)
        {
            commandMessage returnMsg = new commandMessage();
            byte[] msgLen = new byte[4];
            Int32 bufferCnt = 0;

            System.Buffer.BlockCopy(buf, bufferCnt, msgLen, 0, msgLen.Length);
            int messageLength = BitConverter.ToInt32(msgLen, 0);
            bufferCnt += msgLen.Length;

            if (messageLength == bufBytes)
            {
                byte[] addressBytes = new byte[4];
                byte[] portBytes = new byte[sizeof(Int32)];
                byte[] cmdBytes = new byte[sizeof(Int32)];
                
                System.Buffer.BlockCopy(buf, bufferCnt, addressBytes, 0, addressBytes.Length);
                bufferCnt += addressBytes.Length;

                System.Buffer.BlockCopy(buf, bufferCnt, portBytes, 0, portBytes.Length);
                bufferCnt += portBytes.Length;

                System.Buffer.BlockCopy(buf, bufferCnt, cmdBytes, 0, cmdBytes.Length);
                bufferCnt += cmdBytes.Length;

                returnMsg.peerIP = new IPAddress(addressBytes);
                returnMsg.port = BitConverter.ToInt32(portBytes, 0);
                returnMsg.command = BitConverter.ToInt32(cmdBytes, 0);
            }

            return returnMsg;
        }

        private commandMessage createCommandMessage(Int32 peerIndex, Int32 msgInt)
        {
            commandMessage returnMsg = new commandMessage();
            returnMsg.peerIP = peerList[peerIndex].peerIP;
            returnMsg.port = peerList[peerIndex].peerPort;
            returnMsg.command = msgInt;

            return returnMsg;
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {

            if ((e.BytesTransferred == 0) && (e.RemoteEndPoint == null))
            {
                //socket went away.  Break from function
                CloseClientSocket(e);
                return;
            }

            Random randomNumberGenerator = new Random();

            // check if the remote host closed the connection
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            
            byte[] myBuffer = new byte[1501];
            System.Buffer.BlockCopy(e.Buffer, e.Offset, myBuffer, 0, e.Count);

            commandMessage msg = parseCommandMessage(myBuffer, e.BytesTransferred);

            int peerNumber;
            //create peer variable to send back to client
            commandMessage replyMsg = new commandMessage(); 

            //bug bug - do a real calc here
            int clientMsgStreamLength = 16;

            //copy to byte array
            byte[] intBytes = BitConverter.GetBytes(clientMsgStreamLength);
            byte[] addressBytes = new byte[4];
            byte[] portBytes = new byte[4];
            byte[] cmdBytes = new byte[4];
            
            switch (msg.command)
            {
                case 1:
                    peerInstance newPeer = new peerInstance();

                    newPeer.peerIP = msg.peerIP;
                    newPeer.peerPort = msg.port;

                    if (peerList.Count < 2)
                        peerNumber = 0;
                    else
                        peerNumber = randomNumberGenerator.Next(peerList.Count);

                    //add the peer to peerList
                    peerList.Add(new peerInstance());
                    int newPeerCnt = peerList.Count - 1;

                    peerList[newPeerCnt].peerIP = newPeer.peerIP;
                    peerList[newPeerCnt].peerPort = newPeer.peerPort;

					networkLinks netLink = new networkLinks();
                    Console.WriteLine("New p2p machine at {0}.", peerList[newPeerCnt].peerIP);
					if (peerList.Count == 1)
					{
						Console.WriteLine("Machine at {0} doesn't have a server.  First machine in network.", peerList[newPeerCnt].peerIP);
						netLink.client = peerList[newPeerCnt].peerIP;
					   	netLink.server = IPAddress.Parse("0.0.0.0");
						peerConnections.Add (netLink);
					    printConnectionTable();
					}
					else
					{
						Console.WriteLine("Machine at {0}.  Server is {1}.  Port is {2}.",  peerList[newPeerCnt].peerIP, peerList[peerNumber].peerIP, peerList[peerNumber].peerPort);
						netLink.client = peerList[newPeerCnt].peerIP;
						netLink.server = peerList[peerNumber].peerIP;
						peerConnections.Add (netLink);
						printConnectionTable();
					}
					
                    intBytes = BitConverter.GetBytes(16);
				
					if (peerNumber == newPeerCnt)
						addressBytes = (IPAddress.Parse("0.0.0.0")).GetAddressBytes();
					else
                    	addressBytes = peerList[peerNumber].peerIP.GetAddressBytes();
				
				    portBytes = BitConverter.GetBytes(peerList[peerNumber].peerPort);
					cmdBytes = BitConverter.GetBytes(0);

					System.Buffer.BlockCopy(intBytes, 0, myBuffer, 0, 4);  //prepends length to buffer
                    System.Buffer.BlockCopy(addressBytes, 0, myBuffer, 4, addressBytes.Length);
                    System.Buffer.BlockCopy(portBytes, 0, myBuffer, 4 + addressBytes.Length, portBytes.Length);
                    System.Buffer.BlockCopy(cmdBytes, 0, myBuffer, 4 + addressBytes.Length + portBytes.Length, cmdBytes.Length);
                    System.Buffer.BlockCopy(myBuffer, 0, e.Buffer, e.Offset, myBuffer.Length);
                    break;

                case 0:
                    replyMsg = msg;
                    replyMsg.command = 0;

                    intBytes = BitConverter.GetBytes(16);
                    addressBytes = replyMsg.peerIP.GetAddressBytes();
				    portBytes = BitConverter.GetBytes(replyMsg.port);
					cmdBytes = BitConverter.GetBytes(replyMsg.command);

					System.Buffer.BlockCopy(intBytes, 0, myBuffer, 0, 4);  //prepends length to buffer
                    System.Buffer.BlockCopy(addressBytes, 0, myBuffer, 4, addressBytes.Length);
                    System.Buffer.BlockCopy(portBytes, 0, myBuffer, 4 + addressBytes.Length, portBytes.Length);
                    System.Buffer.BlockCopy(cmdBytes, 0, myBuffer, 4 + addressBytes.Length + portBytes.Length, cmdBytes.Length);
                    System.Buffer.BlockCopy(myBuffer, 0, e.Buffer, e.Offset, myBuffer.Length);
                    break;
            }

            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                bool willRaiseEvent = token.Socket.SendAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessSend(e);
                }

            }
            else
            {
                CloseClientSocket(e);
            }
        }

		private void printConnectionTable()
		{
			Console.WriteLine("----------------------------------------------------");
			Console.WriteLine("---------Network looks like-------------------------");
			Console.WriteLine("----------------------------------------------------");
			for (int i=0;i<peerConnections.Count;i++)
			{
				Console.WriteLine("Server {0}\t->\tClient {1}", peerConnections[i].server, peerConnections[i].client);
			}
			Console.WriteLine("----------------------------------------------------\n");
		}
		
		
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                AsyncUserToken token = (AsyncUserToken)e.UserToken;

                // read the next block of data send from the client
                bool willRaiseEvent = token.Socket.ReceiveAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessReceive(e);
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = e.UserToken as AsyncUserToken;

            //IPEndPoint iep = (IPEndPoint)token.Socket.RemoteEndPoint;
			
			List<IPEndPoint> validIP = new List<IPEndPoint>();
			
			for (int i=0; i < myAsyncList.Count; i++)
			{
				if (myAsyncList[i].RemoteEndPoint != null)
				{
					
					validIP.Add((IPEndPoint)myAsyncList[i].RemoteEndPoint);
				}
			}
			List<peerInstance> tempPeers = new List<peerInstance>();
			tempPeers = peerList;
			
			if (tempPeers.Count == 1)
			{
				peerList.Clear();
			}
			else
			{
				for (int k=validIP.Count-1; k>=0; k--)
				{
					for (int j=0; j<tempPeers.Count; j++)
					{
						byte[] ip1 = tempPeers[j].peerIP.GetAddressBytes();
						byte[] ip2 = validIP[k].Address.GetAddressBytes();
						if (ip1 == ip2)	
						{
							tempPeers.RemoveAt(j);
						}
					}
				}
			}
			byte[] peerAddressBytes = new byte[4];
			byte[] removedIP = new byte[4];
			
			if (tempPeers.Count != 0)
			{
				removedIP = tempPeers[0].peerIP.GetAddressBytes();
			}
            //token.m_socket.RemoteEndPoint.Address  & token.m_socket.RemoteEndPoint.Port is what need to be removed from peerList 
            for (int i = 0; i < peerList.Count;i++ )
            {
				peerAddressBytes = peerList[i].peerIP.GetAddressBytes();
                if (peerAddressBytes == removedIP) 
                {
                    Console.WriteLine("Peer has quit");
                    peerList.RemoveAt(i);
                }
            }


            try
            {
                token.Socket.Shutdown(SocketShutdown.Send);
            }
                // throws if client process has already closed
            catch (Exception) { }
            token.Socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref numConnectedSockets);
            maxNumberAcceptedClients.Release();
            Console.WriteLine("There are {0} clients connected to the server", numConnectedSockets);
			printConnectionTable();

            // Free the SocketAsyncEventArg so they can be reused by another client
            //bufferManager.freeBuffer(e);
            asyncSocketStack.Push(e);
        }

    }
}