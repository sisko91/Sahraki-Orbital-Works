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

using SharedProjects;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Script States
        enum ScriptState
        {
            Standby,
            Calibrating,
            Active,
        }
        ScriptState currentState = ScriptState.Standby;

        bool initialized = false;

        IMyRemoteControl controller;

        IMyShipConnector connector;

        IMyTerminalBlock reference;

        int calibrateStatus = 0;

        List<IMyThrust> thrustersList = new List<IMyThrust>();

        List<IMyGyro> gyros = new List<IMyGyro>();

        float[] thrusts = new float[6];

        float maxSpeed = 100;

        Dictionary<Base6Directions.Direction, Vector3I> DirectionMap = new Dictionary<Base6Directions.Direction, Vector3I>()
        {
            { Base6Directions.Direction.Up, Vector3I.Up }, 
            { Base6Directions.Direction.Down, Vector3I.Down }, 
            { Base6Directions.Direction.Left, Vector3I.Left }, 
            { Base6Directions.Direction.Right, Vector3I.Right }, 
            { Base6Directions.Direction.Forward, Vector3I.Forward }, 
            { Base6Directions.Direction.Backward, Vector3I.Backward }, 
        };

        Dictionary<Vector3I, Base6Directions.Direction> DirectionReverseMap = new Dictionary<Vector3I, Base6Directions.Direction>()
        {
            { Vector3I.Up, Base6Directions.Direction.Up },
            { Vector3I.Down, Base6Directions.Direction.Down },
            { Vector3I.Left, Base6Directions.Direction.Left },
            { Vector3I.Right, Base6Directions.Direction.Right },
            { Vector3I.Forward, Base6Directions.Direction.Forward },
            { Vector3I.Backward, Base6Directions.Direction.Backward },
        };

        Vector3 D = Vector3.Zero;
        Vector3 I = Vector3.Zero;
        Vector3 AutopilotMoveIndicator = Vector3.Zero;
        Vector3 AutopilotDirectionIndicator = Vector3.Zero;

        Vector3 targetPosition = Vector3.Zero;

        #region Connect With Shared
        void MySave(StringBuilder builder)
        {
            builder.AppendLine(initialized.ToString());
        }

        void MyLoad()
        {
            bool.TryParse(NextStorageLine(), out initialized);
        }

        void MyProgram()
        {
            updates[ScriptState.Standby] = StandBy;
            updates[ScriptState.Calibrating] = Calibrating;
            updates[ScriptState.Active] = Active;

            commands["calibrate"] = Calibrate;
            commands["moveto"] = MoveTo;
            commands["turnto"] = TurnTo;
            commands["setwaypoint"] = SetWaypoint;

            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            Initialize();
        }

        void MySetupCommands(MenuRemote root)
        {

        }

        void Initialize()
        {
            initialized = true;
            GetParts();
        }

        void GetParts()
        {
            controller = null;
            connector = null;

            thrustersList.Clear();
            for (int i = 0; i < thrusts.Length; i++)
                thrusts[i] = 0;

            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectParts);

            if (controller != null)
            {
                controller.IsMainCockpit = true;
                controller.DampenersOverride = true;
            }
            foreach (var thruster in thrustersList)
            {
                var f = thruster.Orientation.Forward;
                thrusts[(int)f] += thruster.MaxEffectiveThrust;
            }
        }

        private bool CollectParts(IMyTerminalBlock block)
        {
            if (!Me.IsSameConstructAs(block)) return false;

            if (block is IMyRemoteControl)
                controller = (IMyRemoteControl)block;

            if (block is IMyShipConnector)
                connector = (IMyShipConnector)block;

            if (block is IMyThrust)
                thrustersList.Add((IMyThrust)block);

            if (block is IMyGyro)
                gyros.Add((IMyGyro)block);

            return false;
        }


        // My Updates Functions
        void StandBy()
        {
            controller.DampenersOverride = true;
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            AutopilotMoveIndicator = Vector3.Zero;
            D = Vector3.Zero;
            I = Vector3.Zero;
            reference = controller;

            echoBuilder.AppendLine(reference.Orientation.TransformDirection(Base6Directions.Direction.Forward).ToString());
            echoBuilder.AppendLine(connector.Orientation.TransformDirection(Base6Directions.Direction.Forward).ToString());
            echoBuilder.AppendLine($"Reference: {GetStatus(reference)}");
            echoBuilder.AppendLine($"Connector: {GetStatus(connector)}");
            echoBuilder.AppendLine($"Connector: {connector.Orientation.Forward}");
            echoBuilder.AppendLine($"Controller: {controller.Orientation.Forward}");

            SetThrusterPowers();
            foreach (var gyro in gyros)
                gyro.GyroOverride = false;
        }      
        
        void Calibrating()
        {
            controller.DampenersOverride = false;
            Runtime.UpdateFrequency = UpdateFrequency.Update100;

            //if (calibrateStatus == 0)
            //    AutopilotMoveIndicator = Vector3.Forward;
            //if (calibrateStatus == 1)
            //    AutopilotMoveIndicator = Vector3.Backward;
            //if (calibrateStatus == 2)
            //    AutopilotMoveIndicator = Vector3.Up;
            //if (calibrateStatus == 3)
            //    AutopilotMoveIndicator = Vector3.Down;
            //if (calibrateStatus == 4)
            //    AutopilotMoveIndicator = Vector3.Left;
            //if (calibrateStatus == 5)
            //    AutopilotMoveIndicator = Vector3.Right;
            if (calibrateStatus == 6)
            {
                AutopilotMoveIndicator = Vector3I.Zero;
                currentState = ScriptState.Standby;
            }

            calibrateStatus += 1;

            echoBuilder.AppendLine($"Calibrating {calibrateStatus}");

            SetGyroPowers();
            SetThrusterPowers();
        }

        void Active()
        {
            controller.DampenersOverride = false;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            GetMovementVectors(targetPosition, controller, reference, thrusts[0], maxSpeed, out AutopilotMoveIndicator, ref D, ref I);

            SetThrusterPowers();
            SetGyroPowers();

            if (AutopilotDirectionIndicator == Vector3.Zero && AutopilotMoveIndicator == Vector3.Zero)
                currentState = ScriptState.Standby;
        }

        // My Command Functions

        void Calibrate()
        {
            calibrateStatus = 0;
            currentState = ScriptState.Calibrating;
        }

        void MoveTo()
        {
            targetPosition = ParseGPS(commandLine.Argument(1));
            AutopilotDirectionIndicator = targetPosition - reference.WorldMatrix.Translation;
            currentState = ScriptState.Active;
        }

        void TurnTo()
        {
            targetPosition = reference.WorldMatrix.Translation;
            AutopilotDirectionIndicator = ParseGPS(commandLine.Argument(1)) - reference.WorldMatrix.Translation;
            currentState = ScriptState.Active;
        }

        void SetWaypoint()
        {
            Waypoint w = Waypoint.DeserializeWaypoint(commandLine.Argument(1));
            SetWaypoint(w);
        }

        void SetWaypoint(Waypoint w)
        {
            if (w.Position != Vector3.One)
                targetPosition = w.Position;
            if (w.Direction != Vector3.One)
                AutopilotDirectionIndicator = w.Direction;
            if (w.MaxSpeed != -1f)
                maxSpeed = w.MaxSpeed;

            if (w.ReferenceMode == "Dock")
                reference = connector;
            else
                reference = controller;

            echoBuilder.AppendLine(targetPosition.ToString());
            currentState = ScriptState.Active;
        }

        // Helpers
        string GetStatus(IMyTerminalBlock block)
        {
            if (block == null) return "LST";
            return "AOK";
        }
        Vector3 ParseGPS(string s)
        {
            var split = s.Split(':');
            return new Vector3(float.Parse(split[2]), float.Parse(split[3]), float.Parse(split[4]));
        }
        #endregion


        #region Shared Scripts

        // Displayer
        List<IMyTextPanel> displays = new List<IMyTextPanel>();
        List<IMyTerminalBlock> surfaceProviders = new List<IMyTerminalBlock>();
        List<int> surfaceIndices = new List<int>();

        // Setup and utility
        bool isSetup = false;
        StringBuilder setupBuilder = new StringBuilder();
        StringBuilder echoBuilder = new StringBuilder();
        MyCommandLine commandLine = new MyCommandLine();
        Dictionary<string, Action> commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        Dictionary<ScriptState, Action> updates = new Dictionary<ScriptState, Action>();
        List<IMyTerminalBlock> getBlocksScratchPad = new List<IMyTerminalBlock>();

        public Program()
        {
            commands["connectdisplay"] = ConnectDisplay;
            commands["disconnectdisplay"] = DisonnectDisplay;
            commands["send"] = SendData;
            commands["getremotecommands"] = RemoteSendCommands;

            MyProgram();
            Load();
        }

        #region Save and Load
        int _currentLine = 0;
        string[] loadArray;
        public void Save()
        {
            StringBuilder storageBuilder = new StringBuilder();

            storageBuilder.AppendLine(displays.Count.ToString());

            for (int i = 0; i < displays.Count; i++)
                storageBuilder.AppendLine(displays[i].EntityId.ToString());

            storageBuilder.AppendLine(surfaceProviders.Count.ToString());

            for (int i = 0; i < surfaceProviders.Count; i++)
                storageBuilder.AppendLine($"{surfaceProviders[i].EntityId.ToString()} {surfaceIndices[i]}");

            MySave(storageBuilder);

            Me.GetSurface(1).WriteText(storageBuilder.ToString());
        }

        public void Load()
        {
            _currentLine = 0;
            var loadBuilder = new StringBuilder();
            Me.GetSurface(1).ReadText(loadBuilder);
            loadArray = loadBuilder.ToString().Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            // First line is the number of hooked displays
            int count = 0;
            int.TryParse(NextStorageLine(), out count);
            for (int i = 0; i < count; i++)
                ConnectDisplay(long.Parse(NextStorageLine()), 0);

            // Number of hooked surface providers
            count = 0;
            int.TryParse(NextStorageLine(), out count);
            for (int i = 0; i < count; i++)
            {
                string[] split = NextStorageLine().Split(' ');
                ConnectDisplay(long.Parse(split[0]), int.Parse(split[1]));
            }

            MyLoad();
        }

        private string NextStorageLine()
        {
            _currentLine += 1;
            if (loadArray.Length >= _currentLine)
                return loadArray[_currentLine - 1];
            return String.Empty;
        }
        #endregion

        public void Main(string argument, UpdateType updateSource)
        {
            if (commandLine.TryParse(argument))
            {
                Action commandAction;

                string command = commandLine.Argument(0);
                if (command == null)
                {
                    Echo("No command specified");
                }
                else if (commands.TryGetValue(command, out commandAction))
                {
                    // We have found a command. Invoke it.
                    commandAction();
                }
                else
                {
                    echoBuilder.AppendLine($"Unknown command {command}");
                }
            }
            else
            {
                echoBuilder.Clear();
                echoBuilder.Append(setupBuilder.ToString());

                Action updateAction;
                if (updates.TryGetValue(currentState, out updateAction))
                {
                    updateAction();
                }

                doDisplay();

            }
        }

        // Helpers
        void doDisplay()
        {
            for (int i = 0; i < displays.Count; i++)
                displays[i].WriteText(echoBuilder.ToString());
            for (int i = 0; i < surfaceProviders.Count; i++)
                ((IMyTextSurfaceProvider)surfaceProviders[i]).GetSurface(surfaceIndices[i]).WriteText(echoBuilder.ToString());

            base.Echo(echoBuilder.ToString());
        }

        private bool SameConstructAsMe(IMyTerminalBlock block)
        {
            return block.IsSameConstructAs(Me);
        }

        // Remote

        struct MenuRemote
        {
            public string name;
            public List<MenuRemote> subMenues;
            public string remoteCommand;

            public MenuRemote(string name, string command)
            {
                this.name = name;
                this.remoteCommand = command;
                this.subMenues = new List<MenuRemote>();
            }

            public string Serialize()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append($"{name}{{");
                for (int i = 0; i < subMenues.Count; i++)
                {
                    string v = subMenues[i].Serialize();
                    builder.Append($"{{{v.Length.ToString()}}}{v}");
                }
                builder.Append($"}}{remoteCommand}");
                return builder.ToString();
            }
        }
        void SendData()
        {
            long targetId;
            string data = commandLine.Argument(1);
            long.TryParse(commandLine.Argument(2), out targetId);
            SendData(targetId, data);
        }

        private void SendData(long targetId, string data)
        {
            ((IMyProgrammableBlock)GridTerminalSystem.GetBlockWithId(targetId)).CustomData = data;
        }

        void RemoteSendCommands()
        {
            long targetId;
            long.TryParse(commandLine.Argument(1), out targetId);

            MenuRemote remoteMenuRoot;

            // Setup remote commands
            remoteMenuRoot = new MenuRemote("root", "");

            // Setup display
            var displaysMenu = new MenuRemote($"Displays ({displays.Count + surfaceProviders.Count})", "");
            displaysMenu.subMenues.Add(GetConnectDisplayRemote());
            displaysMenu.subMenues.Add(GetDisconnectDisplayRemote());

            remoteMenuRoot.subMenues.Add(displaysMenu);

            MySetupCommands(remoteMenuRoot);

            SendData(targetId, remoteMenuRoot.Serialize());
        }

        #region Remote Display Setup
        private MenuRemote GetConnectDisplayRemote()
        {
            MenuRemote connectDisplayRemote = new MenuRemote("Connect >", "");

            // Get surface providers
            GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(getBlocksScratchPad, SameConstructAsMe);
            for (int i = 0; i < getBlocksScratchPad.Count; i++)
            {
                var provider = (IMyTextSurfaceProvider)getBlocksScratchPad[i];
                var subItem = new MenuRemote(getBlocksScratchPad[i].CustomName, "");

                for (int j = 0; j < provider.SurfaceCount; j++)
                {
                    subItem.subMenues.Add(new MenuRemote($"{provider.GetSurface(j).DisplayName} >", $"connectdisplay {getBlocksScratchPad[i].EntityId} {j}"));
                }

                connectDisplayRemote.subMenues.Add(subItem);
            }
            getBlocksScratchPad.Clear();

            // Get panels
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(getBlocksScratchPad, SameConstructAsMe);
            for (int i = 0; i < getBlocksScratchPad.Count; i++)
            {
                var panel = (IMyTextPanel)getBlocksScratchPad[i];
                var subItem = new MenuRemote(getBlocksScratchPad[i].CustomName, "");
                subItem.subMenues.Add(new MenuRemote($"{getBlocksScratchPad[i].DisplayName}", $"connectdisplay {getBlocksScratchPad[i].EntityId} 0"));

                connectDisplayRemote.subMenues.Add(subItem);
            }

            getBlocksScratchPad.Clear();
            return connectDisplayRemote;
        }

        private MenuRemote GetDisconnectDisplayRemote()
        {
            MenuRemote disconnectDisplayRemote = new MenuRemote("Disconnect >", "");

            for (int i = 0; i < surfaceProviders.Count; i++)
                disconnectDisplayRemote.subMenues.Add(new MenuRemote(surfaceProviders[i].CustomName, $"disconnectdisplay {surfaceProviders[i].EntityId}"));
            for (int i = 0; i < displays.Count; i++)
                disconnectDisplayRemote.subMenues.Add(new MenuRemote(((IMyTerminalBlock)displays[i]).CustomName, $"disconnectdisplay {displays[i].EntityId}"));

            return disconnectDisplayRemote;
        }

        void ConnectDisplay()
        {
            ConnectDisplay(long.Parse(commandLine.Argument(1)), int.Parse(commandLine.Argument(2)));
        }

        void ConnectDisplay(long id, int subId)
        {
            try
            {
                var block = GridTerminalSystem.GetBlockWithId(id);
                if (block is IMyTextPanel) displays.Add((IMyTextPanel)block);
                else
                {
                    surfaceProviders.Add(block);
                    surfaceIndices.Add(subId);
                }
            }
            catch (Exception e)
            {
                setupBuilder.AppendLine(e.ToString());
            }
        }

        void DisonnectDisplay()
        {
            DisconnectDisplay(long.Parse(commandLine.Argument(1)));
        }

        void DisconnectDisplay(long id)
        {
            for (int i = 0; i < surfaceProviders.Count; i++)
            {
                if (surfaceProviders[i].EntityId == id)
                {
                    ((IMyTextSurfaceProvider)surfaceProviders[i]).GetSurface(surfaceIndices[i]).WriteText("");
                    surfaceProviders.RemoveAt(i);
                    surfaceIndices.RemoveAt(i);
                    return;
                }
            }

            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i].EntityId == id)
                {
                    displays[i].WriteText("");
                    displays.RemoveAt(i);
                    return;
                }
            }
        }
        #endregion

        #endregion
    }
}
