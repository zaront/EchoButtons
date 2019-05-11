using InTheHand.Net.Sockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EchoButtons
{
	class ButtonListener : IDisposable
	{
		BluetoothClient _client;
		BluetoothDeviceInfo _device;
		Stream _stream;
		ButtonMessageParser _parser;
		bool _reconnect = true;
		byte[] _buffer = new byte[1000];

		public event EventHandler<ButtonEventArgs> Pressed;
		public event EventHandler<ButtonEventArgs> Released;
		public event EventHandler<ButtonEventArgs> Connected;
		public event EventHandler<ButtonEventArgs> Disconnected;

		public string Name { get; }

		public ButtonListener(BluetoothDeviceInfo device)
		{
			//set fields
			_device = device;
			_parser = new ButtonMessageParser();
			_client = new BluetoothClient();
			Name = _device.DeviceName;
		}

		public void Dispose()
		{
			_reconnect = false;
			_client.Close();
			_client.Dispose();
			if (_stream != null)
				_stream.Dispose();
		}

		public void StartListening()
		{
			Reconnect().ContinueWith(i => i);
		}

		async Task Reconnect()
		{
			try
			{
				_client.BeginConnect(_device.DeviceAddress, _device.InstalledServices[3], Connecting, null);
			}
			catch (Exception ex)
			{
				//try again
				if (_reconnect)
				{
					await Task.Delay(1000);
					StartListening();
				}
			}
		}

		void Connecting(IAsyncResult result)
		{
			if (result.IsCompleted)
			{
				_client.EndConnect(result);
				if (_client.Connected)
				{
					//send events
					Connected?.Invoke(this, new ButtonEventArgs() { ButtonName = Name });

					_stream = _client.GetStream();
					ReadEvents();
				}
				else
				{
					//try again
					if (_reconnect)
						StartListening();
				}
			}
		}

		void ReadEvents()
		{
			_stream.BeginRead(_buffer, 0, _buffer.Length, Read, null);
		}

		void Read(IAsyncResult result)
		{
			if (result.IsCompleted)
			{
				var len = _stream.EndRead(result);

				if (len == 0)
				{
					//connection closed

					//send events
					Disconnected?.Invoke(this, new ButtonEventArgs() { ButtonName = Name });

					//start listening again
					if (_reconnect)
					{
						_client.Close();
						_client.Dispose();
						_client = new BluetoothClient(); //create a new client - for some reason its not reusable at this point
						StartListening();
					}
				}
				else
				{
					var messages = _parser.GetMessage(_buffer, len);
					if (messages != null)
					{
						foreach (var message in messages)
						{
							if (message.Payload[1] == 1 && message.Payload[25] == 2)
							{
								//set events
								Pressed?.Invoke(this, new ButtonEventArgs() { ButtonName = Name });
							}

							if (message.Payload[1] == 1 && message.Payload[25] == 3)
							{
								//set events
								Released?.Invoke(this, new ButtonEventArgs() { ButtonName = Name });
							}
						}
					}

					//read again
					ReadEvents();
				}
			}
		}
	}
}
