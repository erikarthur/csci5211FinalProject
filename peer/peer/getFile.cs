using System;
using System.Threading;
using socketSrv;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using ServerExperiment;

namespace peer
{
	public class fileTransport
	{
		public void getFile (object data)
		{
            Console.WriteLine("in fileTransport - get file");
			socketSrv.commandMessage cmd = (socketSrv.commandMessage)data;
			IPEndPoint iep = new IPEndPoint(IPAddress.Any, cmd.port);
			TcpListener server = new TcpListener(iep);
			server.ExclusiveAddressUse = true;
			server.Start();
			TcpClient tcpFIleNetClient = server.AcceptTcpClient();
			
			//IPAddress remoteMachine = IPAddress.Parse(tcpFIleNetClient.Client.RemoteEndPoint.ToString());
			
			//Console.WriteLine("\n{0} Connected.  Starting file transfer\n", remoteMachine);
			
			byte [] buffer = new byte[1500];
			
			NetworkStream fileNetStream = tcpFIleNetClient.GetStream();
			int numBytes = fileNetStream.Read(buffer, 0, 1500);
			
			byte [] messageSizeBytes = new byte[4];
            byte[] addressBytes = new byte[4];
            byte[] portBytes = new byte[4];
			byte [] cmdBytes = new byte[4];
			byte [] fileSizeBytes = new byte[4];
			byte [] fileNameSizeBytes = new byte[4];
			
			int messageSize, fileSize, fileNameSize, cmdNum;
			string fileName;
			
			System.Buffer.BlockCopy(buffer, 0, messageSizeBytes, 0, 4);
            System.Buffer.BlockCopy(buffer, 4, addressBytes, 0, 4);
            System.Buffer.BlockCopy(buffer, 8, portBytes, 0, 4);
			System.Buffer.BlockCopy(buffer, 12, cmdBytes, 0, 4);
			System.Buffer.BlockCopy(buffer, 16, fileSizeBytes, 0, 4);	
			System.Buffer.BlockCopy(buffer, 20, fileNameSizeBytes, 0, 4);
			
			messageSize = BitConverter.ToInt32(messageSizeBytes,0);
			fileSize = BitConverter.ToInt32(fileSizeBytes, 0);
            UTF8Encoding utf8 = new UTF8Encoding();

            fileNameSize = BitConverter.ToInt32(fileNameSizeBytes, 0);
			cmdNum = BitConverter.ToInt32(cmdBytes,0);
			if (cmdNum != cmd.command)
			{
				//lucy we have a problem
				//bugbug
			}
            byte[] fileNameBytes = new byte[fileNameSize];	
			System.Buffer.BlockCopy(buffer, 24, fileNameBytes, 0, fileNameSize);
			
			fileName = utf8.GetString(fileNameBytes);

			int bytesLeft = (24 + fileNameSize + fileSize);
            BinaryWriter fs = new BinaryWriter(File.Open(cmd.fileDir  + fileName, FileMode.CreateNew));
            
            fs.Write(buffer, 24 + fileNameSize, numBytes - (24 + fileNameSize));

            int recBytes = numBytes;

            while (recBytes < bytesLeft)  //may need messageSize here
			{
                numBytes = fileNetStream.Read(buffer, 0, 1500);
                fs.Write(buffer, 0, numBytes);
                recBytes += numBytes;
                Console.Write("*");
			}
            fs.Close();

            Console.WriteLine("\nFile transfer complete. {0} bytes written\n", recBytes);
			
			fileNetStream.Close();
			tcpFIleNetClient.Close();
            Program.p2p.refreshFileList(cmd.fileDir);
            //p2p.refreshFileList(cmd.fileDir);
		}
		
		public void sendFile (object data)
		{
			Console.WriteLine("in file transport - send file");
			
			socketSrv.commandMessage cmd = (socketSrv.commandMessage)data;
			
			Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			
			IPEndPoint iep = new IPEndPoint(cmd.peerIP, cmd.port);
			sock.Connect(iep);
			//TcpClient tcpSendFile = new TcpClient(iep);
			NetworkStream netStream = new NetworkStream(sock);
			
			byte [] messageSizeBytes = new byte[4];
			byte [] addressBytes = new byte[4];
			byte [] portBytes = new byte[4];
			byte [] cmdBytes = new byte[4];
			byte [] fileSizeBytes = new byte[4];
			byte [] fileNameSizeBytes = new byte[4];
			byte [] fileNameBytes = new byte[255];
			byte[] buffer = new byte[1500];
			int messageSize, fileSize, fileNameSize, bufCnt;
			string fileName;
			
			FileInfo fi1 = new FileInfo(cmd.fileName);
			if (fi1.Exists)
			{
				fileSize = (int)fi1.Length;
				fileName = fi1.Name;
				fileNameSize = fileName.Length;
			}
			else
				return;
			
			bufCnt = 0;
			
			messageSizeBytes = BitConverter.GetBytes(0);
			System.Buffer.BlockCopy(messageSizeBytes, 0, buffer, 0,  4);
			bufCnt += 4;

			addressBytes = cmd.peerIP.GetAddressBytes();
			System.Buffer.BlockCopy(addressBytes, 0, buffer, bufCnt, 4);
			bufCnt += 4;

			portBytes = BitConverter.GetBytes(cmd.port);
			System.Buffer.BlockCopy(portBytes, 0, buffer, bufCnt, 4);
			bufCnt += 4;
			
			cmdBytes = BitConverter.GetBytes(cmd.command);
			System.Buffer.BlockCopy(cmdBytes, 0, buffer, bufCnt, 4);
			bufCnt += 4;
			
			fileSizeBytes = BitConverter.GetBytes(fileSize);
			System.Buffer.BlockCopy(fileSizeBytes, 0, buffer, bufCnt, 4);	
			bufCnt += 4;
				
			UTF8Encoding encoder = new UTF8Encoding();
			
			fileNameSize = encoder.GetByteCount(fileName);
				
			fileNameSizeBytes = BitConverter.GetBytes(fileNameSize);
			System.Buffer.BlockCopy(fileNameSizeBytes, 0, buffer, bufCnt, 4);
			bufCnt += 4;
			
			fileNameBytes = encoder.GetBytes(fileName);
			System.Buffer.BlockCopy(fileNameBytes, 0, buffer, bufCnt, fileNameSize);
			bufCnt += fileNameSize;
            int totalByteCnt = bufCnt;	
			using (BinaryReader fs = new BinaryReader(File.Open(cmd.fileName, FileMode.Open)))
	        {
	            int readCnt = fs.Read(buffer,bufCnt,buffer.Length-bufCnt);
                messageSizeBytes = BitConverter.GetBytes(bufCnt);
                System.Buffer.BlockCopy(messageSizeBytes, 0, buffer, 0, 4);
	            while (readCnt > 0)
	            {
					bufCnt += readCnt;
                    totalByteCnt += readCnt;
                    Console.Write("*");
					netStream.Write(buffer,0,bufCnt);
					bufCnt = 0;
					readCnt = fs.Read(buffer,bufCnt,buffer.Length);
	            }
	        }
            Console.WriteLine("File sent.  {0} bytes put on wire.", totalByteCnt);
			netStream.Close();
			sock.Close();
			return;	
		}
	}
}

