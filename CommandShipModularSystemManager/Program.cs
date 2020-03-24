﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using VRage.Library;



namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public Program()
        {
            subsystemManager = new SubsystemManager(this);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            ParseConfigs();

            // Add subsystems
            // Intel system setup
            IIntelProvider intelSubsystem;
            if (IsMaster) intelSubsystem = new IntelMasterSubsystem();
            else intelSubsystem = new IntelSlaveSubsystem();
            
            subsystemManager.AddSubsystem("intel", (ISubsystem)intelSubsystem);
            
            // Looking Glass Setup
            LookingGlassNetworkSubsystem lookingGlassNetwork = new LookingGlassNetworkSubsystem(intelSubsystem, "LG", !FixedLookingGlass, ThrusterLookingGlass);
            subsystemManager.AddSubsystem("lookingglass", lookingGlassNetwork);
            
            // Hangar system setup
            HangarSubsystem hangarSubsystem = new HangarSubsystem(intelSubsystem);
            subsystemManager.AddSubsystem("hangar", hangarSubsystem);
            
            // Seeing-Eye scanner setup
            subsystemManager.AddSubsystem("scanner", new ScannerNetworkSubsystem(intelSubsystem, "SE"));

            // Inventory system setup
            //InventoryManagerSubsystem inventorySubsystem = new InventoryManagerSubsystem();
            //inventorySubsystem.RegisterRequester(hangarSubsystem);
            //subsystemManager.AddSubsystem("inventory", inventorySubsystem);

            // Command system setup
            TextCommandSubsystem textCommandSubsystem = new TextCommandSubsystem(intelSubsystem);
            subsystemManager.AddSubsystem("command", textCommandSubsystem);

            // Drone Forge setup
            subsystemManager.AddSubsystem("forge", new DroneForgeSubsystem(intelSubsystem));

            subsystemManager.DeserializeManager(Storage);
        }

        bool IsMaster = false;
        bool FixedLookingGlass = false;
        bool ThrusterLookingGlass = false;
        string FleetTag = "";
        // [Setup]
        // IsMaster = true
        // FixedLookingGlass = false
        // ThrusterLookingGlass = false
        // FleetTag = ""
        private void ParseConfigs()
        {
            MyIni Parser = new MyIni();
            MyIniParseResult result;
            if (!Parser.TryParse(Me.CustomData, out result))
                return;

            IsMaster = Parser.Get("Setup", "IsMaster").ToBoolean();
            FixedLookingGlass = Parser.Get("Setup", "FixedLookingGlass").ToBoolean();
            ThrusterLookingGlass = Parser.Get("Setup", "ThrusterLookingGlass").ToBoolean();
        }

        MyCommandLine commandLine = new MyCommandLine();

        SubsystemManager subsystemManager;

        public void Save()
        {
            string v = subsystemManager.SerializeManager();
            Storage = v;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            subsystemManager.UpdateTime();
            if (commandLine.TryParse(argument))
            {
                subsystemManager.Command(commandLine.Argument(0), commandLine.Argument(1), commandLine.ArgumentCount > 2 ? commandLine.Argument(2) : null);
            }
            else
            {
                subsystemManager.Update(updateSource);
                Echo(subsystemManager.GetStatus());
            }
        }
    }
}
