﻿using System;
using System.Text;
using Terraria_Server.Collections;

namespace Terraria_Server.Messages
{
    public class InventoryDataMessage : IMessage
    {
        public Packet GetPacket()
        {
            return Packet.INVENTORY_DATA;
        }

        public void Process(int start, int length, int num, int whoAmI, byte[] readBuffer, byte bufferData)
        {
            int playerIndex = readBuffer[start + 1];
            
            if (playerIndex != whoAmI)
            {
                Netplay.slots[whoAmI].Kick ("Cheating detected (INVENTORY_DATA forgery).");
                return;
            }
        
            playerIndex = whoAmI;

            if (playerIndex != Main.myPlayer)
            {
                Player player = Main.players[playerIndex];
                lock (player)
                {
                    int inventorySlot = (int)readBuffer[start + 2];
                    int stack = (int)readBuffer[start + 3];
                    String itemName = Encoding.ASCII.GetString(readBuffer, start + 4, length - 4);
                    Item item = Registries.Item.Create(itemName, stack);
                    if (inventorySlot < 44)
                    {
                        player.inventory[inventorySlot] = item;
                    }
                    else
                    {
                        player.armor[inventorySlot - 44] = item;
                    }

                    if (Program.server.RejectedItemsContains(itemName) ||
                        Program.server.RejectedItemsContains(item.Type.ToString()))
                    {
                        player.Kick((itemName.Length > 0) ? itemName : item.Type + " is not allowed on this server.");
                    }

                    NetMessage.SendData(5, -1, whoAmI, itemName, playerIndex, (float)inventorySlot);
                }
            }
        }
    }
}
