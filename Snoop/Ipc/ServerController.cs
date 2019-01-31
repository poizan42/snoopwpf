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

namespace Snoop.Ipc
{
	enum ServerCommands
	{
		FreeObjRef
	}
	class ServerController
	{
		private Stream serverComm;

		public event Action<Exception> FatalError;

		public ServerController(Stream serverComm)
		{
			this.serverComm = serverComm;
		}

		public void FreeObjRef(long objRef) =>
			this.PostMessage(ServerCommands.FreeObjRef, __arglist(objRef));

		private unsafe void PostMessage(ServerCommands cmd, __arglist)
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
				this.serverComm.BeginWrite(ms.GetBuffer(), 0, (int)ms.Length, CheckWriteError, null);
			}
		}

		private void CheckWriteError(IAsyncResult ar)
		{
			try
			{
				serverComm.EndWrite(ar);
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
