using InTheHand.Net.Sockets;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EchoButtons
{
	public class EchoButton : IDisposable
	{
		const string _buttonPrefix = "EchoBtn";
		BluetoothClient _scanner;
		ButtonListener[] _buttons;


		public event EventHandler<ButtonEventArgs> Pressed;
		public event EventHandler<ButtonEventArgs> Released;
		public event EventHandler<ButtonEventArgs> FoundPairedDevice;
		public event EventHandler<ButtonEventArgs> NoPairedDevices;
		public event EventHandler<ButtonEventArgs> Connected;
		public event EventHandler<ButtonEventArgs> Disconnected;

		public void Dispose()
		{
			StopListening();
		}

		public void StartListening()
		{
			//create bluetooth adapter
			if (_scanner == null)
				_scanner = new BluetoothClient();

			//scan for paired deviced
			if (_buttons == null)
				ScanForDevices();
		}

		void ScanForDevices()
		{
			_scanner.BeginDiscoverDevices(50, true, true, false, false, FoundDevices, null);
		}

		void FoundDevices(IAsyncResult result)
		{
			if (result.IsCompleted)
			{
				var devices = _scanner.EndDiscoverDevices(result);
				if (devices != null)
				{
					var buttons = new List<ButtonListener>();
					foreach (var buttonInfo in devices.Where(i => i.DeviceName.StartsWith(_buttonPrefix)).ToArray())
					{
						var button = new ButtonListener(buttonInfo);
						button.Connected += Button_Connected;
						button.Disconnected += Button_Disconnected;
						button.Pressed += Button_Pressed;
						button.Released += Button_Released;
						buttons.Add(button);
					}
						
					if (buttons.Count != 0)
						_buttons = buttons.ToArray();

					//sent events
					if (_buttons != null)
					{
						foreach (var button in _buttons)
							FoundPairedDevice?.Invoke(this, new ButtonEventArgs() { ButtonName = button.Name });
					}
					else
						NoPairedDevices?.Invoke(this, new ButtonEventArgs());

					//start listening to buttons
					if (buttons != null)
						foreach (var button in buttons)
							button.StartListening();
				}
			}
		}

		public void StopListening()
		{
			//close all streams and clients
			if (_buttons != null)
			{
				foreach (var button in _buttons)
				{
					button.Dispose();
					button.Connected -= Button_Connected;
					button.Disconnected -= Button_Disconnected;
					button.Pressed -= Button_Pressed;
					button.Released -= Button_Released;
				}
				_buttons = null;
			}
		}

		private void Button_Released(object sender, ButtonEventArgs e)
		{
			Released?.Invoke(this, e);
		}

		private void Button_Pressed(object sender, ButtonEventArgs e)
		{
			Pressed?.Invoke(this, e);
		}

		private void Button_Disconnected(object sender, ButtonEventArgs e)
		{
			Disconnected?.Invoke(this, e);
		}

		private void Button_Connected(object sender, ButtonEventArgs e)
		{
			Connected?.Invoke(this, e);
		}
	}

	public class ButtonEventArgs : EventArgs
	{
		public string ButtonName { get; set; }
	}
}
