using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using peer;
using ServerExperiment;

namespace socketSrv
{
    class Client
    {
        TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
        peerInstance myServer = new peerInstance();
        NetworkStream clientStream;
		public Timer clientTimer = new Timer();
		//clientTimer.ElapsedEventArgs += timer

		public List<commandMessage> clientQueue = new List<commandMessage>();

		public Client()
		{
			this.clientTimer.Elapsed += new ElapsedEventHandler (timer_Elapsed);
		}

        public void setServer(peerInstance p)
        {
            myServer.peerIP = p.peerIP;
            myServer.peerPort = p.peerPort;
        }
		
        public bool isClientMessage()
        {
            bool returnBool = false;
            if (clientQueue.Count > 0)
                returnBool = true;

            return returnBool;
        }


		public List<commandMessage> returnClientQueue()
		{
			checkForData();
			List<commandMessage> tempQueue = new List<commandMessage>();
			tempQueue = clientQueue;
			
            //if (clientQueue.Count > 1)
            //{
				
            //    //lock(serverQueue)
            //    //{
            //    clientQueue.Clear();
            //    //}
            //}
			
			
			return tempQueue;
		}
		
		public void checkForData()
		{
			commandMessage cmd = new commandMessage();
			cmd.command = Int32.MaxValue;
			
			int bufSize = 1500;
			byte[] buffer = new byte[bufSize];
			if (clientStream.DataAvailable)
			{
				byte [] messageSizeBytes = new byte[4];
            	byte[] addressBytes = new byte[4];
            	byte[] portBytes = new byte[4];
				byte [] cmdBytes = new byte[4];
				byte [] fileSizeBytes = new byte[4];
				byte [] fileNameSizeBytes = new byte[4];
                byte[] srcIpBytes = new byte[4];
				
			
				int messageSize, fileSize, fileNameSize, cmdNum, byteCnt;
				IPAddress cmdIP;
				string fileName;
				
				//gotta process the data
				int bytesRead = clientStream.Read(buffer,0,bufSize);
				if (bytesRead > 0)
				{
					
				byteCnt = 0;
					System.Buffer.BlockCopy(buffer, byteCnt, messageSizeBytes, 0, messageSizeBytes.Length);
					byteCnt += messageSizeBytes.Length;
					messageSize = BitConverter.ToInt32(messageSizeBytes,0);
					
					//messageSize should never be greater than 1500 in this case
					System.Buffer.BlockCopy(buffer, byteCnt, addressBytes, 0, addressBytes.Length);
					byteCnt += addressBytes.Length;
					//cmdIP = IPAddress.Parse(
					string address = "";
					if (addressBytes.Length == 4)
					{
					    address = addressBytes[0].ToString() + "." + addressBytes[1].ToString() + "." +
							addressBytes[2].ToString() + "." + addressBytes[3].ToString();
					}
					
					cmd.peerIP = IPAddress.Parse(address);
					
		            System.Buffer.BlockCopy(buffer, byteCnt, portBytes, 0, portBytes.Length);
					byteCnt += portBytes.Length;
					cmd.port = BitConverter.ToInt32(portBytes,0);
					
					System.Buffer.BlockCopy(buffer, byteCnt, cmdBytes, 0, cmdBytes.Length);
					byteCnt += cmdBytes.Length;
					cmd.command = BitConverter.ToInt32(cmdBytes,0);                                 
					
					System.Buffer.BlockCopy(buffer, byteCnt, fileSizeBytes, 0, fileSizeBytes.Length);
					byteCnt += fileSizeBytes.Length;
					fileSize = BitConverter.ToInt32(fileSizeBytes, 0);
					
					System.Buffer.BlockCopy(buffer, byteCnt, fileNameSizeBytes, 0, fileNameSizeBytes.Length);
					byteCnt += fileNameSizeBytes.Length;
					fileNameSize = BitConverter.ToInt32(fileNameSizeBytes, 0);
					
		            UTF8Encoding utf8 = new UTF8Encoding();
	
		            byte[] fileNameBytes = new byte[fileNameSize];	
					System.Buffer.BlockCopy(buffer, byteCnt, fileNameBytes, 0, fileNameSize);
					
					cmd.fileName = utf8.GetString(fileNameBytes);

                    byteCnt += fileNameSize;

                    System.Buffer.BlockCopy(buffer, byteCnt, srcIpBytes, 0, srcIpBytes.Length);
                    
                    address = "";
                    if (srcIpBytes.Length == 4)
                    {
                        address = addressBytes[0].ToString() + "." + addressBytes[1].ToString() + "." +
                            addressBytes[2].ToString() + "." + addressBytes[3].ToString();
                    }

                    cmd.srcIP = IPAddress.Parse(address);
					Console.WriteLine("in client process. Command is {0} and srcIP is {1}", cmd.command, cmd.srcIP); 
					clientQueue.Add(cmd);
                    //Program.p2p.clientProcessedQueue.Add(cmd);
				}
			}
				
			
			
		}
		
        public void connectToServer()
        {
            IPHostEntry serverIP = Dns.GetHostEntry(myServer.peerIP.ToString());
            tcpClient = new TcpClient(serverIP.HostName, myServer.peerPort);

            clientStream = tcpClient.GetStream();

            //Int32 messageID = 1;   //add client
            //int clientMsgStreamLength = (int)(4 * sizeof(Int32));
            //byte[] buffer = new byte[4096];

            //byte[] intBytes = BitConverter.GetBytes(clientMsgStreamLength);
            //byte[] addressBytes = ServerExperiment.Program.myAddress.GetAddressBytes();
            //byte[] portBytes = BitConverter.GetBytes(ServerExperiment.Program.myPort);
            //byte[] messageBytes = BitConverter.GetBytes(messageID);

            //System.Buffer.BlockCopy(intBytes, 0, buffer, 0, intBytes.Length);  //prepends length to buffer
            //System.Buffer.BlockCopy(addressBytes, 0, buffer, 4, addressBytes.Length);
            //System.Buffer.BlockCopy(portBytes, 0, buffer, 8, portBytes.Length);
            //System.Buffer.BlockCopy(messageBytes, 0, buffer, 12, messageBytes.Length);

            //clientStream.Write(buffer, 0, clientMsgStreamLength);
            //clientStream.Flush();

            ////wait for ACK from server
            //int bytesRead = clientStream.Read(buffer, 0, 4096);

            //byte[] message = new byte[4092];
            //byte[] messageLength = new byte[4];
            //int numMessageBytes = 0;
            //int nextMsgBytesRead = 0;

            //if (bytesRead > 3)
            //{
            //    //strip off first 4 bytes and get the message length
            //    System.Buffer.BlockCopy(buffer, 0, messageLength, 0, sizeof(Int32));
            //    numMessageBytes = BitConverter.ToInt32(messageLength, 0);
            //}

            //while (bytesRead < numMessageBytes)
            //{
            //    nextMsgBytesRead = clientStream.Read(buffer, bytesRead, 4096 - bytesRead);
            //    bytesRead += nextMsgBytesRead;

            //    //bugbug - need a watchdog timer for timeouts
            //    //bugbug - need to handle the case of more data than expected from the network
            //}


            //byte[] ackMessage = new byte[4];
            //int ackNum = -99;
            //System.Buffer.BlockCopy(buffer, 4, ackMessage, 0, 4);
            //ackNum = BitConverter.ToInt32(ackMessage, 0);
        }

        public void SendCmd (socketSrv.commandMessage cmd)
        {
			byte [] buffer = new byte[1500];
			byte [] cmdBytes = new byte[4];
			byte [] msgLenBytes = new byte[4];
			byte [] addressBytes = new byte[4];
			byte [] portBytes = new byte[4];
            byte[]  fileNameBytes = new byte[75];
			byte[]  fileNameSizeBytes = new byte[4];
			byte[]  fileSizeBytes = new byte[4];
            byte[] srcIpBytes = new byte[4];
			
            //switch( cmd.command)
            //{
            //case 2:    //get file
				
                //check processedQueue for cmd
                    for (int i = 0; i < Program.p2p.serverProcessedQueue.Count;i++)
                    {
                        if ((Program.p2p.serverProcessedQueue[i].peerIP.Address == cmd.peerIP.Address) && 
                        (Program.p2p.serverProcessedQueue[i].fileName == cmd.fileName))
                        {
                            //Console.WriteLine("Got duplicate cmd.  Aborting send.");
                            return;
                        }
                    }

                    Console.WriteLine("\nSent request to server machine");

				    clientTimer.Interval = 5000;
				    clientTimer.Start ();

                    //int basicCmdLen = 16;
                    int byteCnt = 4;

				    cmdBytes = BitConverter.GetBytes(cmd.command);
                    //msgLenBytes = BitConverter.GetBytes(basicCmdLen);
				    addressBytes = cmd.peerIP.GetAddressBytes();
				    portBytes = BitConverter.GetBytes(cmd.port);
                    //System.Buffer.BlockCopy(msgLenBytes, 0, buffer, byteCnt, msgLenBytes.Length);
                    //byteCnt += msgLenBytes.Length;

                    System.Buffer.BlockCopy(addressBytes, 0, buffer, byteCnt, addressBytes.Length);
                    byteCnt += addressBytes.Length;

                    System.Buffer.BlockCopy(portBytes, 0, buffer, byteCnt, portBytes.Length);
                    byteCnt += portBytes.Length;

                    System.Buffer.BlockCopy(cmdBytes, 0, buffer, byteCnt, cmdBytes.Length);
                    byteCnt += cmdBytes.Length;
			
                    UTF8Encoding utf8 = new UTF8Encoding();
                    fileNameBytes = utf8.GetBytes(cmd.fileName);
                    int fileNameLen = utf8.GetByteCount(cmd.fileName);
			
				    fileSizeBytes = BitConverter.GetBytes(0);
				    fileNameSizeBytes = BitConverter.GetBytes(fileNameLen);
				
				    System.Buffer.BlockCopy(fileSizeBytes, 0, buffer, byteCnt, fileSizeBytes.Length);
                    byteCnt += fileSizeBytes.Length;
				
				    System.Buffer.BlockCopy(fileNameSizeBytes, 0, buffer, byteCnt, fileNameSizeBytes.Length);
                    byteCnt += fileNameSizeBytes.Length;

                    System.Buffer.BlockCopy(fileNameBytes, 0, buffer, byteCnt, fileNameLen);
                    byteCnt += fileNameLen;

                    srcIpBytes = Program.p2p.myAddress.GetAddressBytes();
                    System.Buffer.BlockCopy(srcIpBytes, 0, buffer, byteCnt, srcIpBytes.Length);
                    byteCnt += srcIpBytes.Length;

                    int msgLen = byteCnt;
                    msgLenBytes = BitConverter.GetBytes(msgLen);
                    System.Buffer.BlockCopy(msgLenBytes, 0, buffer, 0, msgLenBytes.Length);

                    //if (cmd.srcIP.Address.ToString() == "0.0.0.0")
                    cmd.srcIP = Program.p2p.myAddress;

                    if ((cmd.srcIP.Address != cmd.peerIP.Address) || (Program.p2p.serverProcessedQueue.Count == 0))
                    {
                        clientStream.Write(buffer, 0, msgLen);
                        Console.WriteLine("sent a message of {0} bytes asking for file", msgLen);
                    }
                    //add command to serverProcessedQueue
                    Program.p2p.serverProcessedQueue.Add(cmd);
				    //break;

            //case 3:     //put file
            //    Console.WriteLine("got a put");
            //    break;

            //}

        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
			DateTime now = DateTime.Now;
			clientTimer.Stop ();
			//find server messages older than 1 minute
			for (int i = Program.p2p.serverProcessedQueue.Count-1; i >= 0; i--) {
				if (Program.p2p.serverProcessedQueue [i].timeStamp.AddMinutes (1) >= now) {
                    Console.WriteLine("{0} file not received socket timed out.", Program.p2p.serverProcessedQueue[i].fileName);
					Program.p2p.serverProcessedQueue.RemoveAt (i);
				}
			}
        }
    }

}
