using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Terraria_Server.Logging;

namespace Terraria_Server.Messages
{
    public class ReceivingPlayerJoinedMessage : IMessage
    {
        public Packet GetPacket()
        {
            return Packet.RECEIVING_PLAYER_JOINED;
        }

        public void Process(int start, int length, int num, int whoAmI, byte[] readBuffer, byte bufferData)
        {
            int playerIndex = readBuffer[num];
            
            if (playerIndex != whoAmI)
            {
                Netplay.slots[whoAmI].Kick ("Cheating detected (RECEIVING_PLAYER_JOINED forgery).");
                return;
            }

            playerIndex = whoAmI;
            num++;
            
            Player player = Main.players[playerIndex];
            
            if (player.SpawnX >= 0 && player.SpawnY >= 0)
            {
                player.OldSpawnX = player.SpawnX;
                player.OldSpawnY = player.SpawnY;
            }

            player.SpawnX = BitConverter.ToInt32(readBuffer, num);
            num += 4;
            player.SpawnY = BitConverter.ToInt32(readBuffer, num);
            num += 4;
            player.Spawn();
            
            //ProgramLog.Debug.Log ("sx: {0}, sy: {1}, tx: {2}, ty: {3}", player.SpawnX, player.SpawnY, player.TeleSpawnX, player.TeleSpawnY);
            
            if (Netplay.slots[whoAmI].state >= SlotState.SENDING_TILES)
            {
                if (Netplay.slots[whoAmI].state == SlotState.SENDING_TILES)
                {
                    Netplay.slots[whoAmI].state = SlotState.PLAYING;
                    NetMessage.OnPlayerJoined (whoAmI); // this also forwards the message
                }
                else
                {
                    var msg = NetMessage.PrepareThreadInstance ();
                    if (player.TeleSpawnX != -1 && player.SpawnX == -1 && player.SpawnY == -1)
                    {
                        msg.ReceivingPlayerJoined (whoAmI, player.TeleSpawnX, player.TeleSpawnY);
                    }
                    else if (player.TeleSpawnX != -1 && (player.SpawnX != player.TeleSpawnX || player.SpawnY != player.TeleSpawnY))
                    {
                        if (player.TeleRetries < 3)
                        {
                            ProgramLog.Debug.Log ("Player teleported to bed, retrying.");
                            player.TeleRetries += 1;
                            player.Teleport (player.TeleSpawnX, player.TeleSpawnY, true);
                            return;
                        }
                        else
                        {
                            ProgramLog.Debug.Log ("Player teleported to bed, giving up.");
                            msg.ReceivingPlayerJoined (whoAmI);
                        }
                    }
                    else
                        msg.ReceivingPlayerJoined (whoAmI);

                    player.TeleportDone ();

                    msg.BroadcastExcept (whoAmI);
                }
            }
        }
    }
}
