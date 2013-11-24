
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace socketSrv
{
    class BufferManager
    {
        int numBytes;                 
        byte[] buffer;                
        Stack<int> bufferStack;      
        int bufferSize;
        int maxBuffers;

        public BufferManager(int totalBytes, int bSize, int numBuffers)
        {
            numBytes = totalBytes;
            bufferSize = bSize;
            maxBuffers = numBuffers;
            bufferStack = new Stack<int>(maxBuffers);
        }

        public void initBuffers()
        {
            buffer = new byte[numBytes];

            for (int i=0; i<maxBuffers;i++)
            {
                bufferStack.Push(i * bufferSize * 2);
            }
        }

        public bool assignBuffer(SocketAsyncEventArgs asyncSocket)
        {

            if (bufferStack.Count > 0)
            {
                asyncSocket.SetBuffer(buffer, bufferStack.Pop(), bufferSize);
               
            }
            return true;
        }

        public void freeBuffer(SocketAsyncEventArgs asyncSocket)
        {
            bufferStack.Push(asyncSocket.Offset);
            asyncSocket.SetBuffer(null, 0, 0);
        }
    }
}