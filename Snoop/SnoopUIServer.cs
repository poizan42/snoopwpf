// (c) Copyright 2019 Kasper F. Brandt.
// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Infrastructure;
using Snoop.Ipc;

namespace Snoop
{
	public class SnoopUIServer
	{
		private NamedPipeClientStream clientComm;
		private ClientController clientController;

		public SnoopUIServer(NamedPipeClientStream clientComm)
		{
			this.clientComm = clientComm;
			this.clientController = new ClientController(clientComm);
		}
		#region Public Static Methods

		// ReSharper disable once UnusedMember.Global
		public static bool GoBabyGo()
		{
			try
			{
				SnoopApplication();
				return true;
			}
			catch (Exception exception)
			{
				ErrorDialog.ShowDialog(exception, "Error Snooping", "There was an error snooping the application.", exceptionAlreadyHandled: true);
				return false;
			}
		}

		public static void SnoopApplication()
		{
			Dispatcher dispatcher;
			if (Application.Current == null)
			{
				dispatcher = Dispatcher.CurrentDispatcher;
			}
			else
			{
				dispatcher = Application.Current.Dispatcher;
			}

			if (dispatcher.CheckAccess())
			{
				var pipeStream = new NamedPipeClientStream(".", "snoop-" + System.Diagnostics.Process.GetCurrentProcess().Id,
					PipeDirection.InOut, PipeOptions.Asynchronous);
				pipeStream.Connect(SnoopModes.ConnectTimeout);
				SnoopUIServer snoopServer = new SnoopUIServer(pipeStream);
				snoopServer.Init();
			}
			else
			{
				dispatcher.Invoke((Action)SnoopApplication);
				return;
			}
		}

		private void Init()
		{
			ServerHandler serverHandler = new ServerHandler(this.clientComm, clientController, this);
			serverHandler.Run();
			var title = TryGetMainWindowTitle();
			this.clientController.NotifyMainWindowTitle(title);

			this.Inspect();

			//CheckForOtherDispatchers(Dispatcher.CurrentDispatcher);
		}

		public void Inspect()
		{
			var foundRoot = this.FindRoot();
			if (foundRoot == null)
			{
				if (!SnoopModes.MultipleDispatcherMode)
				{
					//SnoopModes.MultipleDispatcherMode is always false for all scenarios except for cases where we are running multiple dispatchers.
					//If SnoopModes.MultipleDispatcherMode was set to true, then there definitely was a root visual found in another dispatcher, so
					//the message below would be wrong.
					MessageBox.Show
					(
						"Can't find a current application or a PresentationSource root visual.",
						"Can't Snoop",
						MessageBoxButton.OK,
						MessageBoxImage.Exclamation
					);
				}

				return;
			}

			this.Inspect(foundRoot/*, SnoopWindowUtils.FindOwnerWindow(null)*/);
		}

		public void Inspect(object rootToInspect/*, Window ownerWindow*/)
		{
			this.clientController.Inspect(rootToInspect/*, new WindowInteropHelper(ownerWindow).Handle*/);
			/*this.Dispatcher.UnhandledException += this.UnhandledExceptionHandler;

			this.Load(rootToInspect);

			this.Owner = ownerWindow;

			SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);

			this.Show();
			this.Activate();*/
		}


		private object FindRoot()
		{
			object foundRoot = null;

			if (SnoopModes.MultipleDispatcherMode)
			{
				foreach (PresentationSource presentationSource in PresentationSource.CurrentSources)
				{
					if
					(
						presentationSource.RootVisual != null &&
						presentationSource.RootVisual is UIElement &&
						((UIElement)presentationSource.RootVisual).Dispatcher.CheckAccess()
					)
					{
						foundRoot = presentationSource.RootVisual;
						break;
					}
				}
			}
			else if (Application.Current != null)
			{
				foundRoot = Application.Current;
			}
			else
			{
				// if we don't have a current application,
				// then we must be in an interop scenario (win32 -> wpf or windows forms -> wpf).


				// in this case, let's iterate over PresentationSource.CurrentSources,
				// and use the first non-null, visible RootVisual we find as root to inspect.
				foreach (PresentationSource presentationSource in PresentationSource.CurrentSources)
				{
					if
					(
						presentationSource.RootVisual != null &&
						presentationSource.RootVisual is UIElement &&
						((UIElement)presentationSource.RootVisual).Visibility == Visibility.Visible
					)
					{
						foundRoot = presentationSource.RootVisual;
						break;
					}
				}
			}

			return foundRoot;
		}


		/*private void CheckForOtherDispatchers(Dispatcher mainDispatcher)
		{
			// check and see if any of the root visuals have a different mainDispatcher
			// if so, ask the user if they wish to enter multiple mainDispatcher mode.
			// if they do, launch a snoop ui for every additional mainDispatcher.
			// see http://snoopwpf.codeplex.com/workitem/6334 for more info.

			List<Visual> rootVisuals = new List<Visual>();
			List<Dispatcher> dispatchers = new List<Dispatcher>();
			dispatchers.Add(mainDispatcher);
			foreach (PresentationSource presentationSource in PresentationSource.CurrentSources)
			{
				Visual presentationSourceRootVisual = presentationSource.RootVisual;

				if (!(presentationSourceRootVisual is Window))
					continue;

				Dispatcher presentationSourceRootVisualDispatcher = presentationSourceRootVisual.Dispatcher;

				if (dispatchers.IndexOf(presentationSourceRootVisualDispatcher) == -1)
				{
					rootVisuals.Add(presentationSourceRootVisual);
					dispatchers.Add(presentationSourceRootVisualDispatcher);
				}
			}

			if (rootVisuals.Count > 0)
			{
				var result =
					MessageBox.Show
					(
						"Snoop has noticed windows running in multiple dispatchers.\n\n" +
						"Would you like to enter multiple dispatcher mode, and have a separate Snoop window for each dispatcher?\n\n" +
						"Without having a separate Snoop window for each dispatcher, you will not be able to Snoop the windows in the dispatcher threads outside of the main dispatcher. " +
						"Also, note, that if you bring up additional windows in additional dispatchers (after Snooping), you will need to Snoop again in order to launch Snoop windows for those additional dispatchers.",
						"Enter Multiple Dispatcher Mode",
						MessageBoxButton.YesNo,
						MessageBoxImage.Question
					);

				if (result == MessageBoxResult.Yes)
				{
					SnoopModes.MultipleDispatcherMode = true;
					Thread thread = new Thread(new ParameterizedThreadStart(DispatchOut));
					thread.Start(rootVisuals);
				}
			}
		}*/

		/*private static void DispatchOut(object o)
		{
			List<Visual> visuals = (List<Visual>)o;
			foreach (var v in visuals)
			{
				// launch a snoop ui on each dispatcher
				v.Dispatcher.Invoke
				(
					(Action)
					(
						() =>
						{
							SnoopUI snoopOtherDispatcher = new SnoopUI();
							snoopOtherDispatcher.Inspect(v, v as Window);
						}
					)
				);
			}
		}*/
		#endregion
		private static string TryGetMainWindowTitle()
		{
			if (Application.Current != null && Application.Current.MainWindow != null)
			{
				return Application.Current.MainWindow.Title;
			}
			return string.Empty;
		}

	}
}
