using System;
using System.Collections.Generic;
using System.Text;

namespace EchoButtons
{
	// https://developer.amazon.com/docs/alexa-gadgets-toolkit/exchange-packets.html

	class ButtonMessageParser
	{
		internal const byte SOP = 0xF0;
		internal const byte EOP = 0xF1;
		internal const byte ESP = 0xF2;

		byte[] packet_buffer = new byte[1024];
		int packet_buffer_len = 0;
		bool packet_error = false;
		bool start_of_packet = false;
		bool end_of_packet = false;
		bool escape = false;
		int packet_size = 0;

		int _sequenceID;

		public ButtonMessage[] GetMessage(byte[] data, int len)
		{
			return ConvertMessage(data, len);
		}

		public byte[] GetBytes(ButtonMessage message)
		{
			//set the sequence
			message.SequenceID = (byte)_sequenceID++;

			//set checksum
			message.Checksum = CalculateChecksum(message);

			return ConvertToByte(message);
		}

		Byte[] ConvertToByte(ButtonMessage message)
		{
			var index = 0;
			var result = new byte[message.Payload.Length + 7];
			result[index++] = message.StartOfFrame;
			result[index++] = message.PacketID;
			result[index++] = message.ErrorID;
			result[index++] = message.SequenceID;
			foreach (var data in message.Payload)
				result[index++] = data;
			var checksum = BitConverter.GetBytes(message.Checksum);
			result[index++] = checksum[1];
			result[index++] = checksum[0];
			result[index++] = message.EndOfFrame;

			return result;
		}

		void Cleanup()
		{
			packet_buffer_len = 0;
			packet_error = false;
			start_of_packet = false;
			end_of_packet = false;
			escape = false;
			packet_size = 0;
			packet_buffer = new byte[1024];
		}

		ushort CalculateChecksum(ButtonMessage message)
		{
			//payload_len: The actual length of the packet
			//subtracting 2 here, to remove checksum from len
			//else we will end up computing checksum of the checksum
			ushort checksum = 0;
			checksum += (ushort)message.PacketID;
			checksum += (ushort)message.ErrorID;

			foreach (var d in message.Payload)
				checksum += (ushort)d;

			return checksum;
		}

		ButtonMessage ParseMessage()
		{
			var result = new ButtonMessage();
			int index = 0;
			result.StartOfFrame = packet_buffer[index++];

			if (result.StartOfFrame != SOP)
				return null; //Invalid SOP

			result.PacketID = packet_buffer[index++];
			result.ErrorID = packet_buffer[index++];
			result.SequenceID = packet_buffer[index++];

			int index2 = 0;
			result.Payload = new byte[packet_size - index - 3];
			for (; index < packet_size - 3; index++)
			{
				result.Payload[index2++] = packet_buffer[index];
			}

			result.EndOfFrame = packet_buffer[index + 2];

			if (result.EndOfFrame != EOP)
				return null; //Invalid EOP

			//correct checksum (to big endian)
			result.Checksum = BitConverter.ToUInt16(new byte[] { packet_buffer[index + 1], packet_buffer[index] }, 0);

			//verify checksum
			var check = CalculateChecksum(result);

			if (check != result.Checksum)
				return null; //Invalid Checksum

			return result;
		}

		// This function is to demonstrate how to extract one packet from the receive buffer
		ButtonMessage[] ConvertMessage(byte[] buffer, int buffer_len)
		{
			var result = new List<ButtonMessage>();

			// parse data from stream buffer
			int index = 0;
			for (index = 0; index < buffer_len; ++index)
			{
				// process every byte in the stream buffer
				if (packet_buffer_len >= 1024)
				{
					// packet too big or 0xf1 is dropped, we cannot process this packet
					if (packet_error == false)
					{
						packet_error = true; //Framing Error: Packet buffer overrun
					}
				}

				if (packet_error == true)
				{
					if (buffer[index] != 0xF0)
					{
						continue;
					}
					// if we found error, drop bytes until we see SOP
					Cleanup();
				}

				switch (buffer[index])
				{
					case SOP:
						if (start_of_packet == true)
						{
							// SOP already received, this double SOP is not allowed
							packet_error = true; //Framing Error: Received multiple SOP
							continue;
						}
						if (packet_buffer_len != 0)
						{
							// SOP received out of order, it should be the start of packet.
							packet_error = true; //Framing Error: SOP received in the middle of a packet
							continue;
						}

						// no error
						start_of_packet = true;
						packet_buffer[packet_buffer_len++] = SOP;
						++packet_size;
						break;
					case EOP: // EOP
						end_of_packet = true;
						packet_buffer[packet_buffer_len++] = EOP;
						++packet_size;
						break;
					case ESP: // ESP
						escape = true;
						break;
					default: // not special byte
						if (escape == true)
						{
							// unescape
							buffer[index] ^= ESP;
							escape = false;
						}
						packet_buffer[packet_buffer_len++] = buffer[index];
						++packet_size;
						break;
				}

				if (end_of_packet == true)
				{
					if (start_of_packet == false)
					{
						packet_error = true; //Framing Error: unexpected end of packet
						continue;
					}
					else
					{
						// we got a full packet
						var data = ParseMessage();
						// clean up for next packet processing
						Cleanup();

						if (data != null)
							result.Add(data);
					}
				}
			}

			if (result.Count == 0)
				return null;
			return result.ToArray();
		}

	}

	class ButtonMessage
	{
		//header
		public byte StartOfFrame = ButtonMessageParser.SOP;
		public byte PacketID = 0x02;
		public byte ErrorID = 0x00;
		public byte SequenceID;

		//payload
		public byte[] Payload;

		//footer
		public ushort Checksum;
		public byte EndOfFrame = ButtonMessageParser.EOP;
	}

	class ButtonState
	{
		public int CommandID;
		public string SerialID;
		public int State;

	}
}
