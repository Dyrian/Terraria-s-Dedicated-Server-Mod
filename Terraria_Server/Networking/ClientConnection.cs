using System;
using System.Net.Sockets;
using System.Collections.Generic;

using Terraria_Server.Logging;

namespace Terraria_Server.Networking
{
	public class ClientConnection : Connection
	{
		int assignedSlot = -1;
		int messageLength = 0;
		int indexInAll = -1;
		
		byte[] sideBuffer;
		int    sideBytes;
		int    sideLength;
		
		volatile SlotState state = SlotState.VACANT;
		
		public SlotState State
		{
			get { return state; }
			set { state = value; }
		}
		
		public int SlotIndex
		{
			get { return assignedSlot; }
			internal set { assignedSlot = value; }
		}
		
		public static List<ClientConnection> All { get; private set; }
		
		static ClientConnection ()
		{
			All = new List<ClientConnection> ();
		}
		
		public ClientConnection (Socket socket, int slot) : base(socket)
		{
			//var buf = NetMessage.buffer[id];
			//socket.SendBufferSize = 128000;
			assignedSlot = slot;
			socket.LingerState = new LingerOption (true, 10);
			lock (All)
			{
				indexInAll = All.Count;
				All.Add (this);
			}
			StartReceiving (new byte [4192]);
		}
		
		public override void Send (byte[] bytes)
		{
			base.Send (bytes);
		}
		
		public void ProcessSideBuffer ()
		{
			DecodeMessages (sideBuffer, ref sideBytes, ref sideLength);
			sideBuffer = null;
			sideBytes = 0;
			sideLength = 0;
		}
		
		protected override void ProcessRead ()
		{
			//ProgramLog.Log ("Read (total={0}).", recvBytes);
			try
			{
				DecodeMessages (recvBuffer, ref recvBytes, ref messageLength);
			}
			catch (Exception e)
			{
				ProgramLog.Log (e, string.Format ("Error processing read from client {0} @ {1}", RemoteAddress, assignedSlot));
				Kick ("Server malfunction, please reconnect.");
			}
			//ProgramLog.Log ("After read (total={0}).", recvBytes);
		}
		
		protected override void HandleClosure (SocketError err)
		{
			if (assignedSlot >= 0)
			{
				ProgramLog.Users.Log ("{0} @ {1}: connection closed ({2}).", RemoteAddress, assignedSlot, err);
				Netplay.slots[assignedSlot].Reset ();
				assignedSlot = -1;
			}
			else
				ProgramLog.Users.Log ("{0}: connection closed ({1}).", RemoteAddress, err);
			
			FreeSectionBuffer ();
			
			lock (All)
			{
				if (indexInAll == All.Count - 1)
				{
					All.RemoveAt (All.Count - 1);
				}
				else
				{
					var other = All[All.Count - 1];
					other.indexInAll = indexInAll;
					All[indexInAll] = other;
					All.RemoveAt (All.Count - 1);
				}
			}
		}
		
		NetMessage sectionBuffer;
		
		protected void FreeSectionBuffer ()
		{
			if (sectionBuffer != null)
			{
				var buf = sectionBuffer;
				sectionBuffer = null;
				FreeSectionBuffer (buf);
			}
		}

#if TEST_COMPRESSION
		public bool myClient = false;
		
		static long _compressed = 0;
		static long _uncompressed = 0;
#endif

		protected override ArraySegment<byte> SerializeMessage (Message msg)
		{
			switch (msg.kind)
			{
				case 3:
				{
					// TODO: optimize further
					var buf = TakeSectionBuffer ();
					var sX = (msg.param >> 16) * 200;
					var sY = (msg.param & 0xffff) * 150;
					
#if TEST_COMPRESSION
					int uncompressed = 0;
#endif
					
					for (int y = sY; y < sY + 150; y++)
					{
#if TEST_COMPRESSION
						if (myClient)
						{
							uncompressed += buf.TileRowSize (200, sX, y);
							buf.TileRowCompressed (200, sX, y);
						}
						else
#endif
							buf.SendTileRow (200, sX, y);
						
					}
					
#if TEST_COMPRESSION
					if (uncompressed > 0)
					{
						var c = System.Threading.Interlocked.Add (ref _compressed, buf.Written);
						var u = System.Threading.Interlocked.Add (ref _uncompressed, uncompressed);
						ProgramLog.Debug.Log ("Total section compression ratio: {2:0.00}% ({0:0.0}MB -> {1:0.0}MB)", u/1024.0/1024.0, c/1024.0/1024.0, c * 100.0 / u);
					}
#endif
					
					sectionBuffer = buf;
					//ProgramLog.Debug.Log ("{0} @ {1}: Sending section ({2}, {3}) of {4} bytes.", RemoteAddress, assignedSlot, sX, sY, buf.Segment.Count);
					
					return buf.Segment;
				}
			}
			return new ArraySegment<byte> ();
		}
		
		protected override void MessageSendCompleted ()
		{
			FreeSectionBuffer ();
		}
		
		public void DecodeMessages (byte[] readBuffer, ref int totalData, ref int msgLen)
		{
			int processed = 0;
			
			if (totalData >= 4)
			{
				if (msgLen == 0)
				{
					msgLen = BitConverter.ToInt32 (readBuffer, 0) + 4;
					
					if (msgLen <= 4 || msgLen > 4096)
					{
						Kick ("Client sent invalid network message (" + msgLen + ")");
						msgLen = 0;
						return;
					}
				}
				while (totalData >= msgLen + processed && msgLen > 0)
				{
					if (state == SlotState.PLAYER_AUTH && msgLen > 4
						&& (Packet) readBuffer[processed + 4] != Packet.PASSWORD_RESPONSE)
					{
						// put player packets aside until password response
						
						if (sideBytes + msgLen > 4096)
						{
							Kick ("Player data too big.");
							return;
						}
						
						if (sideBuffer == null) sideBuffer = new byte [4096];
						
						Buffer.BlockCopy (readBuffer, processed, sideBuffer, sideBytes, msgLen);
						
						sideBytes += msgLen;
					}
					else
					{
						var slot = assignedSlot;
						if (slot >= 0)
							NetMessage.buffer[slot].GetData (readBuffer, processed + 4, msgLen - 4);
						else
							return;
						
						if (kicking) return;
					}
						

					processed += msgLen;
					if (totalData - processed >= 4)
					{
						msgLen = BitConverter.ToInt32 (readBuffer, processed) + 4;
						
						if (msgLen <= 4 || msgLen > 4096)
						{
							Kick ("Client sent invalid network message (" + msgLen + ")");
							msgLen = 0;
							return;
						}
					}
					else
					{
						msgLen = 0;
					}
				}
				if (processed == totalData)
				{
					totalData = 0;
				}
				else
				{
					if (processed > 0)
					{
						Buffer.BlockCopy (readBuffer, processed, readBuffer, 0, totalData - processed);
						totalData -= processed;
					}
				}
			}
		}

		public void Kick (string reason, bool announce = true)
		{
			if (announce)
			{
				if (assignedSlot >= 0)
				{
					ProgramLog.Admin.Log ("{0} @ {1}: disconnecting for: {2}", RemoteAddress, assignedSlot, reason);
					var player = Main.players[assignedSlot];
					if (player != null) player.DisconnectReason = reason;
				}
				else
					ProgramLog.Admin.Log ("{0}: disconnecting for: {1}", RemoteAddress, reason);
			}
			
			if (! kicking)
			{
				var msg = NetMessage.PrepareThreadInstance ();
				msg.Disconnect (reason);
				KickAfter (msg.Output);

				state = SlotState.KICK;
			}
		}
		
		public void SendSection (int x, int y)
		{
			Send (new Message { kind = 3, param = (x << 16) | (y & 0xffff) });
		}
		
		static Stack<NetMessage> sectionPool = new Stack<NetMessage> ();
		static int sectionPoolCount = 0;
		
		static NetMessage TakeSectionBuffer ()
		{
			lock (sectionPool)
			{
				if (sectionPool.Count > 0)
					return sectionPool.Pop ();
				sectionPoolCount += 1;
			}
			
			ProgramLog.Debug.Log ("Section pool capacity: {0}", sectionPoolCount);
			return new NetMessage (272250);
		}
		
		static void FreeSectionBuffer (NetMessage buf)
		{
			buf.Clear();
			lock (sectionPool)
				sectionPool.Push (buf);
		}
	}
}

