// (c) Copyright 2019 Kasper F. Brandt.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Snoop.Ipc
{
	class ClientHandler
	{
		private readonly Stream serverComm;
		private readonly ServerController serverController;
		private readonly SnoopUI snoopUI;
		public ClientHandler(Stream serverComm, ServerController serverController, SnoopUI snoopUI)
		{
			this.serverComm = serverComm;
			this.serverController = serverController;
			this.snoopUI = snoopUI;
		}

		internal void Run()
		{
			Thread thread = new Thread(this.ReadLoop);
			thread.Name = "ClientHandler";
			thread.IsBackground = true;
			thread.Start();
		}

		private void ReadLoop()
		{
			using (BinaryReader br = new BinaryReader(serverComm))
			{
				while (true)
				{
					this.ReadCommand(br);
				}
			}
		}

		private void ReadCommand(BinaryReader br)
		{
			ClientCommands cmd = (ClientCommands)br.ReadInt32();
			switch (cmd)
			{
				case ClientCommands.NotifyMainWindowTitle:
					this.NotifyMainWindowTitle(br.ReadString());
					return;
				case ClientCommands.Inspect:
					this.Inspect(br.ReadInt64());
					return;
			}
		}
		private void BeginInvoke(Action f)
		{
			this.snoopUI.Dispatcher.BeginInvoke(f);
		}

		private void Inspect(long rootToInspectObjRef)
		{
			this.BeginInvoke(() =>
				this.snoopUI.Inspect(new ObjectRef(this.serverController, rootToInspectObjRef)));
		}

		private void NotifyMainWindowTitle(string title)
		{
			this.BeginInvoke(() =>
				snoopUI.Title = string.Format("{0} - Snoop", title));
		}

	}
}
