
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using peer;
using ServerExperiment;
using System.Timers;

namespace socketSrv
{
  
    class Server : IDisposable
    {
        public List<commandMessage> serverQueue;  // = new Queue<commandMessage>();
		public List<commandMessage> serverMsgList = new List<commandMessage>(); 
        private int numConnections;                     // the maximum number of connections the sample is designed to handle simultaneously 
        private int receiveBufferSize;                  // buffer size to use for each socket I/O operation 
        BufferManager bufferManager;                    // represents a large reusable set of buffers for all socket operations
        const int opsToPreAlloc = 2;                    // read, write (don't alloc buffer space for accepts)
        Socket listenSocket;                            // the socket used to listen for incoming connection requests
        SocketAsyncEventArgsStack asyncSocketStack;     // stack of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
        int numConnectedSockets;                        // the total number of clients connected to the server 
        Semaphore maxNumberAcceptedClients;
        public List<peerInstance> peerList;
		public List<SocketAsyncEventArgs> myAsyncList = new List<SocketAsyncEventArgs>();
        public int serverPort;
		public System.Timers.Timer serverTimer;

        public Server(int numConns, int receiveSize)
        {
			this.numConnectedSockets = 0;
			this.numConnections = numConns;
			this.receiveBufferSize = receiveSize;
			this.bufferManager = new BufferManager(receiveBufferSize * numConnections * opsToPreAlloc, receiveBufferSize, numConnections);
			this.asyncSocketStack = new SocketAsyncEventArgsStack(numConnections);
			this.maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
			this.peerList = new List<peerInstance>();
			this.serverPort = 0;
			this.serverQueue = new List<commandMessage>();
			this.serverTimer = new System.Timers.Timer ();
			this.serverTimer.Elapsed += new ElapsedEventHandler (timer_Elapsed);
			//commandMessage m = new commandMessage();
			//serverQueue.Enqueue(m);
			//m = serverQueue.Dequeue();

        }

        public void Dispose()
        {
            this.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool isServerMessage()
        {
            bool returnBool = false;
            if (serverQueue.Count > 0)
                returnBool = true;

            return returnBool;
        }

		public List<commandMessage> returnServerQueue()
		{
//			List<commandMessage> tempQueue = new List<commandMessage>();
//			tempQueue = serverQueue;
//			
//			if (serverQueue.Count > 1)
//			{
//				
//				//lock(serverQueue)
//				//{
//				serverQueue.Clear();
//				//}
//			}
			
			if (serverQueue.Count > 1)
			{
				
				//lock(serverQueue)
				//{
				serverQueue.Clear();
				//}
			}
			
			return serverQueue;
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

            Console.WriteLine("Starting server for p2p network on port {0}\n", localEndPoint.Port);
            
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
			myAsyncList.Add(socketEventArgs);
			
            ((AsyncUserToken)socketEventArgs.UserToken).Socket = e.AcceptSocket;

            IPEndPoint iep = (IPEndPoint)e.AcceptSocket.RemoteEndPoint;
            Console.WriteLine("New Peer is {0}", iep.Address);

            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(socketEventArgs);

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
            byte[] filesize = new byte[4];
            byte[] fileNameSize = new byte[4];
            byte[] fileNameBytes = new byte[75];
            byte[] srcIpBytes = new byte[4];
            byte[] putIpBytes = new byte[4];
            int fileLen;
            UTF8Encoding utf8 = new UTF8Encoding();
            commandMessage returnMsg = new commandMessage();
            byte[] msgLen = new byte[4];
            
            Int32 bufferCnt = 0;

            System.Buffer.BlockCopy(buf, bufferCnt, msgLen, 0, msgLen.Length);
            int messageLength = BitConverter.ToInt32(msgLen, 0);
            bufferCnt += msgLen.Length;
           
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
        
            
            switch (returnMsg.command)
            {
                case 2: 
                  

                    System.Buffer.BlockCopy(buf, bufferCnt, filesize, 0, filesize.Length);
                    bufferCnt += filesize.Length;
                    
                    System.Buffer.BlockCopy(buf, bufferCnt, fileNameSize, 0, fileNameSize.Length);
                    bufferCnt += fileNameSize.Length;
                    fileLen = BitConverter.ToInt32(fileNameSize, 0);

                    System.Buffer.BlockCopy(buf, bufferCnt, fileNameBytes, 0, fileLen);
                    bufferCnt += fileNameSize.Length;
					
                    returnMsg.fileName = utf8.GetString(fileNameBytes, 0, fileLen);
                    bufferCnt += fileLen;
  
                    System.Buffer.BlockCopy(buf, bufferCnt, srcIpBytes, 0, srcIpBytes.Length);
                    bufferCnt += srcIpBytes.Length;
                    returnMsg.srcIP = new IPAddress(srcIpBytes);
					//serverQueue.Enqueue(returnMsg);
				
                    break;
                case 3:
                   
                    System.Buffer.BlockCopy(buf, bufferCnt, filesize, 0, filesize.Length);
                    bufferCnt += filesize.Length;

                    System.Buffer.BlockCopy(buf, bufferCnt, fileNameSize, 0, fileNameSize.Length);
                    bufferCnt += fileNameSize.Length;
                    fileLen = BitConverter.ToInt32(fileNameSize, 0);

                    System.Buffer.BlockCopy(buf, bufferCnt, fileNameBytes, 0, fileLen);
                    //bufferCnt += fileNameSize.Length;
                    returnMsg.fileName = utf8.GetString(fileNameBytes, 0, fileLen);
                    bufferCnt += fileLen;

                    System.Buffer.BlockCopy(buf, bufferCnt, srcIpBytes, 0, srcIpBytes.Length);
                    bufferCnt += srcIpBytes.Length;
                    returnMsg.putIP = new IPAddress(srcIpBytes);

                    //System.Buffer.BlockCopy(buf, bufferCnt, srcIpBytes, 0, srcIpBytes.Length);
                    //bufferCnt += srcIpBytes.Length;
                    //returnMsg.putIP = new IPAddress(srcIpBytes);
                    //serverQueue.Enqueue(returnMsg);

                    break;
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
            
            byte[] myBuffer = new byte[1500];
            System.Buffer.BlockCopy(e.Buffer, e.Offset, myBuffer, 0, e.Count);

            commandMessage msg = parseCommandMessage(myBuffer, e.BytesTransferred);
			//this.serverQueue.Enqueue(msg);
			//serverQueue.Add(msg);

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
                    peerList[newPeerCnt].asyncSocketEvent = e;

                    Console.WriteLine("Peer is connected to {0} at port {1}", peerList[newPeerCnt].peerIP, peerList[newPeerCnt].peerPort);

                    intBytes = BitConverter.GetBytes(16);
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
                case 2:
	                Console.WriteLine("Received GET cmd for {0} from {1}.  Reply on {2}\n", 
	                                  msg.fileName, msg.peerIP, msg.port);
                    
	                //need to send msg to peer
                    string fileDir = Program.p2p.getFileDir();
                    //msg.fileName = fileDir + msg.fileName;
                    int fileNum = Program.p2p.fileLocal(fileDir + msg.fileName);
                    if (fileNum != int.MaxValue)
                    {
                        fileTransport g = new fileTransport();
						msg.fileName = fileDir + msg.fileName;
                        g.sendFile(msg);
                    }
                    else
                    {
 
                        msg.srcIP = Program.p2p.myAddress;
 					    
                        Console.WriteLine("in server process. Command is {0}. peer ip is {2} and srcIP is {1}", msg.command, msg.srcIP, msg.peerIP);
                        Program.p2p.clientQueue.Add(msg);
                    }
					//serverQueue.Add(msg);
	                break;
                case 3:
                    Console.WriteLine("Received PUT cmd for {0} from {1}.  Reply on {2}\n",
                                      msg.fileName, msg.putIP, msg.port);
                    if (msg.putIP.Address == Program.p2p.myAddress.Address)
                    {
                        //file is for me.
                        int pfileIndex = Program.p2p.fileLocal(msg.fileName);
                        if (pfileIndex == int.MaxValue) {
                            msg.fileName = Program.p2p.getFileDir() + msg.fileName;
                            fileTransport g2 = new fileTransport();
                            Thread t2 = new Thread(g2.receivePutFile);
                            t2.Start(msg);
                        }
                    }
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

            IPEndPoint iep = (IPEndPoint)token.Socket.RemoteEndPoint;
            //token.m_socket.RemoteEndPoint.Address  & token.m_socket.RemoteEndPoint.Port is what need to be removed from peerList 
            for (int i = 0; i < peerList.Count;i++ )
            {
                if (peerList[i].peerIP == iep.Address) 
                {
                    Console.WriteLine("Peer {0} has quit", iep.Address);
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
			
			for (int i=0;i<myAsyncList.Count;i++)
			{
				if (myAsyncList[i] == e)
					myAsyncList.RemoveAt(i);
			}
			
            // Free the SocketAsyncEventArg so they can be reused by another client
            bufferManager.freeBuffer(e);
            asyncSocketStack.Push(e);
        }
		
		public void SendCmd(commandMessage cmd)
		{
			byte [] buffer = new byte[1500];
			byte [] cmdBytes = new byte[4];
			byte [] msgLenBytes = new byte[4];
			byte [] addressBytes = new byte[4];
			byte [] portBytes = new byte[4];
            byte[] fileNameSizeBytes = new byte[4];
            byte[] fileNameBytes = new byte[75];
            byte[] fileSizeBytes = new byte[4];
            byte[] srcIpBytes = new byte[4];

            for (int i = 0; i < Program.p2p.clientProcessedQueue.Count; i++)
            {
                if ((Program.p2p.clientProcessedQueue[i].peerIP.Address == cmd.peerIP.Address) &&
                (Program.p2p.clientProcessedQueue[i].fileName == cmd.fileName))
                {
                    Console.WriteLine("Got duplicate cmd.  Aborting send.");
                    return;
                }
            }
			
			Console.WriteLine("Sent request to client machine(s)\n");
			cmd.timeStamp = DateTime.Now;
			Program.p2p.clientProcessedQueue.Add (cmd);
			serverTimer.Interval = 35000;
			serverTimer.Start();

			cmdBytes = BitConverter.GetBytes(cmd.command);
			//msgLenBytes = BitConverter.GetBytes(16);
			addressBytes = cmd.peerIP.GetAddressBytes();
			portBytes = BitConverter.GetBytes(cmd.port);
            fileSizeBytes = BitConverter.GetBytes(0);

            int byteCnt = 4;
			//System.Buffer.BlockCopy(msgLenBytes,0,buffer,0,4);
            System.Buffer.BlockCopy(addressBytes, 0, buffer, byteCnt, addressBytes.Length);
            byteCnt += addressBytes.Length;

            System.Buffer.BlockCopy(portBytes, 0, buffer, byteCnt, portBytes.Length);
            byteCnt += portBytes.Length;

            System.Buffer.BlockCopy(cmdBytes, 0, buffer, byteCnt, cmdBytes.Length);
            byteCnt += cmdBytes.Length;

            System.Buffer.BlockCopy(fileSizeBytes, 0, buffer, byteCnt, fileSizeBytes.Length);
            byteCnt += fileSizeBytes.Length;


            UTF8Encoding utf8 = new UTF8Encoding();
            int fileNameSize = utf8.GetByteCount(cmd.fileName);

            int fileNameLen = utf8.GetByteCount(cmd.fileName);
            fileNameSizeBytes = BitConverter.GetBytes(fileNameLen);
            System.Buffer.BlockCopy(fileNameSizeBytes, 0, buffer, byteCnt, fileNameSizeBytes.Length);
            byteCnt += fileNameSizeBytes.Length;

            fileNameBytes = utf8.GetBytes(cmd.fileName);
            System.Buffer.BlockCopy(fileNameBytes, 0, buffer, byteCnt, fileNameLen);
            byteCnt += fileNameLen;

            srcIpBytes = cmd.putIP.GetAddressBytes();
            System.Buffer.BlockCopy(srcIpBytes, 0, buffer, byteCnt, srcIpBytes.Length);
            byteCnt += srcIpBytes.Length;

            cmd.srcIP = Program.p2p.myAddress;

            int msgLen = byteCnt;
            msgLenBytes = BitConverter.GetBytes(msgLen);
            System.Buffer.BlockCopy(msgLenBytes, 0, buffer, 0, msgLenBytes.Length);

			AsyncUserToken token;   
			
			for (int i=0;i<myAsyncList.Count;i++)
			{
                System.Buffer.BlockCopy(buffer, 0, myAsyncList[i].Buffer, myAsyncList[i].Offset, msgLen);
				token = (AsyncUserToken)myAsyncList[i].UserToken;

                IPEndPoint iep = (IPEndPoint)token.Socket.RemoteEndPoint;
                IPAddress ip = iep.Address;

                if (cmd.srcIP != ip)
                {
                    if (myAsyncList[i].SocketError == SocketError.Success)
                    {
                        token.Socket.Send(buffer, msgLen, SocketFlags.None);
                    }
                    else
                    {
                        CloseClientSocket(myAsyncList[i]);
                    }
                }
			}
			
			return;	
		}

		void timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			DateTime now = DateTime.Now;
			serverTimer.Stop ();
			//find server messages older than 1 minute
			for (int i = Program.p2p.clientProcessedQueue.Count-1; i >= 0; i--) {
				if (Program.p2p.clientProcessedQueue [i].timeStamp.AddMinutes (1) >= now) {
					Program.p2p.clientProcessedQueue.RemoveAt (i);
				}
			}
		}

    }
}