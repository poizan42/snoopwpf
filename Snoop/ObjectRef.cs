// (c) Copyright 2019 Kasper F. Brandt.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using Snoop.Ipc;

namespace Snoop
{
	public class ObjectRef: IDisposable
	{
		internal ServerController ServerController { get; private set; }
		public long Ref { get; private set; }
		internal ObjectRef(ServerController serverController, long objRef)
		{
			this.ServerController = serverController;
			this.Ref = objRef;
		}

		public void Dispose()
		{
			if (Ref == 0)
				return;
			ServerController.FreeObjRef(Ref);
			Ref = 0;
		}

		~ObjectRef()
		{
			Dispose();
		}
	}
}