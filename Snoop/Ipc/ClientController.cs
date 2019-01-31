// (c) Copyright 2019 Kasper F. Brandt.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace Snoop.Ipc
{
	enum ClientCommands
	{
		NotifyMainWindowTitle,
		Inspect
	}
	internal class DispatcherBoundObject
	{
		public object Object;
		public Dispatcher Dispatcher;
		public DispatcherBoundObject(object obj, Dispatcher dispatcher)
		{
			this.Object = obj;
			this.Dispatcher = dispatcher;
		}
	}

	class ClientController
	{
		private Stream clientComm;

		public event Action<Exception> FatalError;

		public ClientController(Stream clientComm)
		{
			this.clientComm = clientComm;
		}

		public void NotifyMainWindowTitle(string mainWindowTitle)
		{
			this.PostMessage(ClientCommands.NotifyMainWindowTitle, __arglist(mainWindowTitle));
		}

		public void Inspect(object rootToInspect/*, IntPtr ownerWindowHandle*/)
		{
			this.PostMessage(ClientCommands.Inspect, __arglist(this.ObjRef(rootToInspect))/*, (int)(long)ownerWindowHandle*/);
		}

		private long ObjRef(object obj)
		{
			if (!(obj is DispatcherObject))
				obj = new DispatcherBoundObject(obj, Dispatcher.CurrentDispatcher);
			return (long)GCHandle.ToIntPtr(GCHandle.Alloc(obj));
		}

		private unsafe void PostMessage(ClientCommands cmd, __arglist)
		{
			ArgIterator args = new ArgIterator(__arglist);
			MemoryStream ms = new MemoryStream();
			using (BinaryWriter bw = new BinaryWriter(ms))
			{
				bw.Write((int)cmd);
				while (args.GetRemainingCount() > 0)
				{
					TypedReference tr = args.GetNextArg();
					Type type = __reftype(tr);
					if (type == typeof(string))
						bw.Write(__refvalue(tr, string));
					else // We assume that the type is primitive or an enum
					{
						// This is the marshalled, or "native" size - but in the cases we care about it should be equal
						// to the "managed" size.
						int size = Marshal.SizeOf(type);
						switch (size)
						{
							case 1:
								bw.Write(*(byte*)&tr);
								break;
							case 2:
								bw.Write(*(short*)&tr);
								break;
							case 4:
								bw.Write(*(int*)&tr);
								break;
							case 8:
								bw.Write(*(long*)&tr);
								break;
							default:
								throw new ArgumentException();
						}
					}
				}
				this.clientComm.BeginWrite(ms.GetBuffer(), 0, (int)ms.Length, CheckWriteError, null);
			}
		}

		private void CheckWriteError(IAsyncResult ar)
		{
			try
			{
				clientComm.EndWrite(ar);
			}
			catch (Exception e)
			{
				this.OnError(e);
			}
		}

		private void OnError(Exception e)
		{
			FatalError?.Invoke(e);
		}

	}
}
