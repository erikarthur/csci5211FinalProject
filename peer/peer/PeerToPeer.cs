
#undef WINDOWS   //comment out for linux or unix


using socketSrv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using peer;
using ServerExperiment;

namespace peer
	
	
{
    class PeerToPeer
    {
        Random RNG = new Random();
        
        TcpClient centralServer = new TcpClient();
        List<FileInfo> myFiles;

        string fileDir;
        string consoleCmd;
        
		Client c;
		Server s;

        public IPAddress myAddress;
        public int myPort;
        public List<string> fileReceived = new List<string>();
		
		public List<commandMessage> serverQueue = new List<commandMessage>();
		public List<commandMessage> clientQueue = new List<commandMessage>();

		public List<commandMessage> serverProcessedQueue = new List<commandMessage>();
		public List<commandMessage> clientProcessedQueue = new List<commandMessage>();

		public int numClients = 0;
 
        #if (WINDOWS)
            const char ENTERKEY = '\r';
        #else
            const char ENTERKEY = '\n';
        #endif

        public void connectCentralServer(string[] args)
        {
            int numConnections = 9;
            int receiveSize = 1500;
                     
            myPort = 4000 + RNG.Next(4000);

            string hostname = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostname);
            IPAddress den = IPAddress.Parse("192.168.1.215");
            myAddress = IPAddress.Parse("0.0.0.0");

            byte[] networkBytes = new byte[4];
            if (hostEntry.AddressList.Length >= 1)
            {

                for (int i=0;i<hostEntry.AddressList.Length;i++)
                {
                    if (hostEntry.AddressList[i].AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
                    {
                        networkBytes = hostEntry.AddressList[i].GetAddressBytes();
                        if (networkBytes[0] != (byte)169)   //169 is an autoIP in my network
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Clear();
                            sb.Append(networkBytes[0]);
                            sb.Append(".");
                            sb.Append(networkBytes[1]);
                            sb.Append(".");
                            sb.Append(networkBytes[2]);
                            sb.Append(".");
                            sb.Append(networkBytes[3]);
                            myAddress = IPAddress.Parse(sb.ToString());
                            if (myAddress.Address == den.Address)
                                break;  //just for my windows machine with multiple NICs in use.
                        }
                    }
                }
            }

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, myPort);

            s = new Server(numConnections, receiveSize);

            s.Init();
            s.Start(localEndPoint);
 
			//connect to central server and get P2P server info            
            peerInstance clientInstance = new peerInstance();            
			clientInstance = connectToCentralServer(args[0], myAddress, myPort);
			
			fileDir = args[1];
			refreshFileList(fileDir);
			
            //make connection

            string tempIP = clientInstance.peerIP.ToString();
           
            if (tempIP != "0.0.0.0")
            {
                Console.WriteLine("My server address is: {0}:{1}", myAddress, myPort);
                Console.WriteLine("My peer server address is: {0}:{1}", clientInstance.peerIP, clientInstance.peerPort);

				//create the client instance
                c = new Client();
                c.setServer(clientInstance);
                c.connectToServer();
                

            }
            else
			
            {
				//first machine don't need a client instance
                Console.WriteLine("First machine in network.");
                Console.WriteLine("My address is: {0}:{1}", myAddress, myPort);
            }
			
        }

        public void runP2PNetwork()
        {
            while (true)
			{
				checkForInput();
				
                
                if (c != null)
				    clientQueue = c.returnClientQueue();

                serverQueue = s.returnServerQueue();
				
				if (serverQueue.Count > 0)
				{
					processQueue(serverQueue, 0);
					serverQueue.Clear();
				}
				
				if (clientQueue.Count > 0)
				{
					processQueue(clientQueue, 1);
					clientQueue.Clear();
				}
				
				//clientQueue = 
			}
        }
		
		public int fileLocal(string fName)
		{
			int fileIndex = int.MaxValue;
			
			for (int i=0; i<myFiles.Count();i++)
			{
				if (myFiles[i].Name == fName)
					fileIndex = i;
			}	
			return fileIndex;
		}

        public string getFileDir()
        {
            return fileDir;
        }
		
		public void processQueue(List<commandMessage> msgQueue, int queue)
		{
			commandMessage msg = new commandMessage();
			for (int i=0;i<msgQueue.Count(); i++)
			{
                msg = msgQueue[i];
                msgQueue.RemoveAt(i);

                if (msg == null)
                    return;
                
				bool serverSent = true;
				bool clientSent = true;

				if (serverProcessedQueue.Count > 0) {
					for (int j=0;j<serverProcessedQueue.Count;j++) {
						if ((msg.peerIP == serverProcessedQueue[j].peerIP) && (msg.fileName == serverProcessedQueue[j].fileName))
							serverSent = false;
					}
				}

				if (clientProcessedQueue.Count > 0) {
					for (int k=0;k<clientProcessedQueue.Count;k++) {
						if ((msg.peerIP == clientProcessedQueue[k].peerIP) && (msg.fileName == clientProcessedQueue[k].fileName))
							clientSent = false;
					}
				}
				
				switch (msg.command)
				{
				case 2:  //get file
						int fileIndex = fileLocal(msg.fileName);
						if (fileIndex != int.MaxValue)
						    {
							    fileTransport g = new fileTransport();
							    msg.fileName = myFiles[fileIndex].FullName;
							    g.sendFile(msg);
						    }
						else 
						    {
							    //need to rebroadcast msg to peers
						    if (queue == 0) 
                                { //server = 0, client = 1
							        if (clientSent) 
                                    {
									if ((c != null) && (numClients > 0))
									        c.SendCmd (msg);

								        s.SendCmd (msg);  
							        }
						        } 
                            else 
                                {
							        if (serverSent) 
                                    {
								        s.SendCmd (msg);
									
									if ((c != null) && (numClients > 0))
									        c.SendCmd (msg);
							        }
						        }
						    }

    						break;
				case 3:  //put file
                       //need to check my put IP against my IP.
						int pfileIndex = fileLocal(msg.fileName);
						if ((pfileIndex == int.MaxValue) && (msg.putIP.Address == this.myAddress.Address))
						    {
                                fileTransport g2 = new fileTransport();
                                Thread t2 = new Thread(g2.receivePutFile);
                                t2.Start(msg);
						    }
						else 
						    {
							//need to rebroadcast msg to peers
						    if (queue == 0) 
                                { //server = 0, client = 1
							        if (clientSent) 
                                    {
								        if (c != null) 
									        c.SendCmd (msg);
                                        if (s.myAsyncList.Count != 0)
								            s.SendCmd (msg);  
							        }
						        } 
                            else 
                                {
							        if (serverSent) 
                                    {
                                        if (s.myAsyncList.Count != 0)
                                            s.SendCmd (msg);
								        
                                        if (c != null)
									        c.SendCmd (msg);
							        }
						        }
						    }

						break;
                
				}
			}
		}
		
       public void checkForInput()
		{
			ConsoleKeyInfo cki = new ConsoleKeyInfo();
			char ch;
			
			if (Console.KeyAvailable) 
			{
				cki = Console.ReadKey(false);

				if (cki.Key == ConsoleKey.Backspace) 
				{
                    if (consoleCmd.Length > 0)
                    {
                        Console.Write(" ");
                        Console.Write("\b");
                        consoleCmd = consoleCmd.Substring(0, consoleCmd.Length - 1);
                    }

				} 
				else 
				{
					ch = cki.KeyChar;
					
					switch (ch) 
					{
					case ENTERKEY:    //this is '\n' on unix.  it's only on Windows that it's \r
						//parse the command and execute the command
                            if (consoleCmd != "")
                            {
                                executeConsoleCommand(consoleCmd);
                                consoleCmd = "";
                            }
						break;

					default:
						consoleCmd += ch.ToString ();
						break;
					}
				}

			}
		}
		public void executeConsoleCommand(string cmd)
		{
			char[] charSeparators = new char[] {' '};
			
			cmd = cmd.TrimEnd();
			cmd = cmd.TrimStart();
			
			string [] cmdParts = cmd.Split(charSeparators, 3, StringSplitOptions.RemoveEmptyEntries); //split on spaces
			
			printMsg(cmdParts[0].ToUpper().Trim());
					
			switch (cmdParts[0].ToUpper().Trim())
			{
			case "QUIT":
				//tell clients, server, and central server that this client is leaving.
				disconnectFromCentralServer (myAddress, myPort);
				Environment.Exit(0);
				break;
				
			case "GET":
				if (cmdParts.Length != 2)
				{
					Console.WriteLine("Syntax Error:\nUsage:\nget filename\n");
				}
				else
				{
					for (int i=0;i<myFiles.Count;i++)
					{
						if (myFiles[i].Name.ToUpper() == cmdParts[1].ToUpper())
						{
							Console.WriteLine("File: {0} is already on the system\n", cmdParts[1]);
							return;
						}
					}

					Console.WriteLine("Received a get for file: " + cmdParts[1] + "\n");
                    socketSrv.commandMessage cmdGetMsg = new socketSrv.commandMessage();
                    cmdGetMsg.command = 2;
                    cmdGetMsg.fileName = cmdParts[1];
                    cmdGetMsg.fileDir = fileDir;
                    cmdGetMsg.peerIP = myAddress;
                    cmdGetMsg.port = 8001 + RNG.Next(3000);
                    cmdGetMsg.peerHostname = Dns.GetHostName();
					
					//create the TCP Listener Port
					fileTransport g = new fileTransport();
					Thread t = new Thread(g.getFile);
					t.Start(cmdGetMsg);
					
					//signal client and servers to send message to their peers.
                    if (c != null)
                        c.SendCmd(cmdGetMsg);
					s.SendCmd(cmdGetMsg);

				}
				break;
				
			case "PUT":
				if (cmdParts.Length != 3)
				{
					Console.WriteLine("Syntax Error:\nUsage:\nput filename machine\n");
				}
				else
				{
					Console.WriteLine("Sending out a put for file: " + cmdParts[1] + "\n");

                    socketSrv.commandMessage cmdPutMsg = new socketSrv.commandMessage();

                    int putFileIndex = fileLocal(cmdParts[1]);
                    if (putFileIndex != int.MaxValue)
                    {
                        cmdPutMsg.command = 3;
                        
                        cmdPutMsg.peerIP = Program.p2p.myAddress; 
                        cmdPutMsg.port = 8001 + RNG.Next(2999);


                        cmdPutMsg.fileName = cmdParts[1];
                        
                        IPHostEntry tempIP = Dns.GetHostEntry(cmdParts[2]);
                        cmdPutMsg.putIP = tempIP.AddressList[0];
                        
                        cmdPutMsg.timeStamp = DateTime.Now;
                        
                        fileTransport g2 = new fileTransport();
                        Thread t2 = new Thread(g2.sendPutFile);
						Console.WriteLine("Opened the socket to put the file");
                        t2.Start(cmdPutMsg);

                        cmdPutMsg.fileName = cmdParts[1];

                        if (c != null)
                            c.SendCmd(cmdPutMsg);
                        
                        s.SendCmd(cmdPutMsg);
                    }
                   
 				}
				break;
				
			case "LIST":
				printFiles();
				break;
				
			case "REFRESH":
				refreshFileList(fileDir);
				break;
			
			default:
				Console.Write("\nUnrecognized command.  Check syntax and re-enter\n");
				break;
			}
		}
		

		
		public void printMsg(string msg)
		{
			Console.WriteLine("Executing command: " + msg);
		}
		
		
		public void refreshFileList(string fDir)
		{
			myFiles = new List<FileInfo> ();
			
			try
			{
				var txtFiles = Directory.EnumerateFiles(fileDir);
	
				foreach (string currentFile in txtFiles)
				{
					myFiles.Add(new FileInfo(currentFile));
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}
			printFiles();
			
		}
		
		public void printFiles()
		{
			Console.WriteLine("\nLocal Files are:");
			
			int fc = myFiles.Count;
			for (int i=0; i<fc; i++) 
			{
				Console.WriteLine (myFiles [i].Name);
			}
			Console.Write("\n");
		}

        public void disconnectFromCentralServer(IPAddress ip, int p)
		{
			NetworkStream clientStream = centralServer.GetStream();
			clientStream.ReadTimeout = System.Threading.Timeout.Infinite;
			peerInstance peer = new peerInstance();
            peer.peerIP = ip;
            peer.peerPort = p;

			int cmd = 0;  //deregister client

			byte[] addressBytes = peer.peerIP.GetAddressBytes();
			byte[] portBytes = BitConverter.GetBytes(peer.peerPort);
			byte[] cmdBytes = BitConverter.GetBytes(cmd);

			int clientMsgStreamLength = (int)(addressBytes.Length + portBytes.Length + sizeof(Int32) + sizeof(Int32));

			//copy to byte array
			byte[] buffer = new byte[4096];  //add 4 bytes for the message length at the front

			byte[] intBytes = BitConverter.GetBytes(clientMsgStreamLength);

			System.Buffer.BlockCopy(intBytes, 0, buffer, 0, 4);  //prepends length to buffer
			System.Buffer.BlockCopy(addressBytes, 0, buffer, 4, addressBytes.Length);
			System.Buffer.BlockCopy(portBytes, 0, buffer, 4 + addressBytes.Length, portBytes.Length);
			System.Buffer.BlockCopy(cmdBytes, 0, buffer, 4 + addressBytes.Length + portBytes.Length, cmdBytes.Length);

			clientStream.Write(buffer, 0, clientMsgStreamLength);
			clientStream.Flush();


			if (true) {
				Console.WriteLine ("Successfully disconnected from central server");
			}

		}

        public peerInstance connectToCentralServer(string csName, IPAddress ip, int p)
        {
            try
            {
               centralServer = new TcpClient(csName, 4000);
            }
            catch (SocketException SE)
            {
                Console.WriteLine(SE.ErrorCode.ToString());
                Console.WriteLine(SE.Message);
                Environment.Exit(0);
            }

            NetworkStream clientStream = centralServer.GetStream();

            peerInstance peer = new peerInstance();
            peer.peerIP = ip;
            peer.peerPort = p;

			int cmd = 1;  //register client

            byte[] addressBytes = peer.peerIP.GetAddressBytes();
            byte[] portBytes = BitConverter.GetBytes(peer.peerPort);
			byte[] cmdBytes = BitConverter.GetBytes(cmd);

            int clientMsgStreamLength = (int)(addressBytes.Length + portBytes.Length + sizeof(Int32) + sizeof(Int32));


            //copy to byte array
            byte[] buffer = new byte[4096];  //add 4 bytes for the message length at the front

            byte[] intBytes = BitConverter.GetBytes(clientMsgStreamLength);

            System.Buffer.BlockCopy(intBytes, 0, buffer, 0, 4);  //prepends length to buffer
            System.Buffer.BlockCopy(addressBytes, 0, buffer, 4, addressBytes.Length);
            System.Buffer.BlockCopy(portBytes, 0, buffer, 4 + addressBytes.Length, portBytes.Length);
			System.Buffer.BlockCopy(cmdBytes, 0, buffer, 4 + addressBytes.Length + portBytes.Length, cmdBytes.Length);

            clientStream.Write(buffer, 0, clientMsgStreamLength);
            clientStream.Flush();

            int bytesRead, nextMsgBytesRead;
            bytesRead = -99;
            bytesRead = clientStream.Read(buffer, 0, 4096);

            byte[] message = new byte[4092];
            byte[] messageLength = new byte[4];
            int messageBytes = 0;

            if (bytesRead > 3)
            {
                //strip off first 4 bytes and get the message length
                System.Buffer.BlockCopy(buffer, 0, messageLength, 0, sizeof(Int32));

                messageBytes = BitConverter.ToInt32(messageLength, 0);
            }

            while (bytesRead < messageBytes)
            {
                nextMsgBytesRead = clientStream.Read(buffer, bytesRead, 4096 - bytesRead);
                bytesRead += nextMsgBytesRead;

                //bugbug - need a watchdog timer for timeouts
                //bugbug - need to handle the case of more data than expected from the network
            }
            byte[] inBuffer = new byte[messageBytes];
            System.Buffer.BlockCopy(buffer, 4, inBuffer, 0, messageBytes - 4);

            addressBytes = new byte[4];
            portBytes = new byte[sizeof(Int32)];
            System.Buffer.BlockCopy(buffer, 4, addressBytes, 0, 4);
            System.Buffer.BlockCopy(buffer, 8, portBytes, 0, 4);

            IPAddress messageIP = new IPAddress(addressBytes);
            Int32 port = BitConverter.ToInt32(portBytes, 0);

            peerInstance myServer = new peerInstance();
            myServer.peerIP = messageIP;
            myServer.peerPort = port;

            return myServer;
        }
    
    }

    
}
