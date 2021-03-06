using System;
using Terraria_Server.Shops;

namespace Terraria_Server.Messages
{
	public class ChestUnlockMessage : IMessage
	{
		public Packet GetPacket()
		{
			return Packet.CHEST_UNLOCK;
		}
	
		public void Process(int start, int length, int num, int whoAmI, byte[] readBuffer, byte bufferData)
		{
			byte playerId = readBuffer[num++];
			
			if (playerId != whoAmI)
			{
				Netplay.slots[whoAmI].Kick ("Cheating detected (CHEST_UNLOCK forgery).");
				return;
			}
			
			byte action = readBuffer[num++];
			
			if (action == 1)
			{
				int x = BitConverter.ToInt32 (readBuffer, num); num += 4;
				int y = BitConverter.ToInt32 (readBuffer, num);
				
				Chest.Unlock (x, y);
				
				NetMessage.SendData (52, -1, whoAmI, "", playerId, action, x, y, 0);
				NetMessage.SendTileSquare (-1, x, y, 2);
			}
		}
	}
}
