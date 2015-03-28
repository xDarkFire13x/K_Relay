﻿using Lib_K_Relay.Networking.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace K_Relay
{
    partial class FrmMain
    {
        private void InitPackets()
        {
            Console.WriteLine("[Packet Serializer] Looking for packets in {0}", Config.Default.PacketDirectory);

            PacketSerializer.SerializePackets(
                Config.Default.PacketDirectory.ToLower().Replace(
                    "%startuppath%", Application.StartupPath));

            foreach (PacketType type in Enum.GetValues(typeof(PacketType)).Cast<PacketType>())
            {
                if (PacketSerializer.GetStructure(type).Type != PacketType.UNKNOWN)
                    treePackets.Nodes.Insert(0, "[Known] " + type.ToString());
                else
                    treePackets.Nodes.Add(type.ToString());
            }
        }

        private void treePackets_AfterSelect(object sender, TreeViewEventArgs e)
        {
            tbxPacketInfo.Text = PacketSerializer.GetStructure(
                (PacketType)Enum.Parse(typeof(PacketType),
                e.Node.Text.Replace("[Known] ", ""))).ToString();
        }

        private void btnOpenPacketsFolder_Click(object sender, EventArgs e)
        {
            Process.Start(
                Config.Default.PacketDirectory.ToLower().Replace(
                    "%startuppath%", Application.StartupPath));
        }
    }
}