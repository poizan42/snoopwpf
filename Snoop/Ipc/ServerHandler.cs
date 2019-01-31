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
using System.Threading;

namespace Snoop.Ipc
{
	class ServerHandler
	{
		private readonly Stream clientComm;
		private readonly ClientController clientController;
		private readonly SnoopUIServer snoopUIServer;
		public ServerHandler(Stream clientComm, ClientController clientController, SnoopUIServer snoopUI)
		{
			this.clientComm = clientComm;
			this.clientController = clientController;
			this.snoopUIServer = snoopUI;
		}

		internal void Run()
		{
			Thread thread = new Thread(this.ReadLoop);
			thread.Name = "ServerHandler";
			thread.IsBackground = true;
			thread.Start();
		}

		private void ReadLoop()
		{
			using (BinaryReader br = new BinaryReader(clientComm))
			{
				while (true)
				{
					this.ReadCommand(br);
				}
			}
		}

		private void ReadCommand(BinaryReader br)
		{
			ServerCommands cmd = (ServerCommands)br.ReadInt32();
			switch (cmd)
			{
				case ServerCommands.FreeObjRef:
					FreeObjRef(br.ReadInt64());
					return;
			}
		}

		private void FreeObjRef(long objRef)
		{
			GCHandle.FromIntPtr((IntPtr)objRef).Free();
		}
	}
}
