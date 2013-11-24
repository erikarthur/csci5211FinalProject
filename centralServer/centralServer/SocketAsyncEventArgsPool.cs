
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace CentralServer
{
    /// <summary>
    /// Represents a stack of SocketAsyncEventArgs objects.  
    /// </summary>
    class SocketAsyncEventArgsStack
    {
        Stack<SocketAsyncEventArgs> asyncSocketStack;

        public SocketAsyncEventArgsStack(int capacity)
        {
            asyncSocketStack = new Stack<SocketAsyncEventArgs>(capacity);
        }

        /// Add a SocketAsyncEventArg instance to the stack
        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
            lock (asyncSocketStack)
            {
                asyncSocketStack.Push(item);
            }
        }

        /// Removes a SocketAsyncEventArgs instance from the pool
        public SocketAsyncEventArgs Pop()
        {
            lock (asyncSocketStack)
            {
                return asyncSocketStack.Pop();
            }
        }

        /// The number of SocketAsyncEventArgs instances in the pool
        public int Count
        {
            get { return asyncSocketStack.Count; }
        }

    }
}