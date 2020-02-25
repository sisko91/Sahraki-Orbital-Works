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



namespace IngameScript
{
    public class LookingGlassNetworkSubsystem : ISubsystem
    {
        #region ISubsystem
        public UpdateFrequency UpdateFrequency { get; set; }
        public void Command(TimeSpan timestamp, string command, object argument)
        {
            if (command == "activateplugin") ActivatePlugin((string)argument);
        }

        public void DeserializeSubsystem(string serialized)
        {
        }

        public string GetStatus()
        {
            return string.Empty;
        }

        public string SerializeSubsystem()
        {
            return string.Empty;
        }

        public void Setup(MyGridProgram program, string name)
        {
            Program = program;
            GetParts();
            UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Update(TimeSpan timestamp, UpdateFrequency updateFlags)
        {
            if ((updateFlags & UpdateFrequency.Update1) != 0)
            {
                UpdateInputs(timestamp);
            }
            if ((updateFlags & UpdateFrequency.Update10) != 0)
            {
                UpdatePlugins(timestamp);
                UpdateActiveLookingGlass();
                UpdateUpdateFrequency();
            }

        }

        #endregion

        public LookingGlassNetworkSubsystem(IIntelProvider intelProvider)
        {
            IntelProvider = intelProvider;
        }

        public IIntelProvider IntelProvider;

        MyGridProgram Program;

        Dictionary<string, ILookingGlassPlugin> Plugins = new Dictionary<string, ILookingGlassPlugin>();
        ILookingGlassPlugin ActivePlugin = null;

        public List<LookingGlass> lookingGlasses = new List<LookingGlass>();
        public LookingGlass ActiveLookingGlass = null;

        public IMyShipController Controller;

        public void AddLookingGlass(LookingGlass lookingGlass)
        {
            lookingGlasses.Add(lookingGlass);
            lookingGlass.Network = this;
        }

        void GetParts()
        {
            Program.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectParts);
        }
        private bool CollectParts(IMyTerminalBlock block)
        {
            if (!Program.Me.IsSameConstructAs(block)) return false;
            if (block is IMyShipController)
                Controller = (IMyShipController)block;
            return false;
        }

        #region plugins
        public void AddPlugin(string name, ILookingGlassPlugin plugin)
        {
            Plugins[name] = plugin;
            plugin.Host = this;
        }

        void ActivatePlugin(string name)
        {
            if (Plugins.ContainsKey(name)) ActivePlugin = Plugins[name];
        }
        #endregion

        #region Inputs
        bool lastADown = false;
        bool lastSDown = false;
        bool lastDDown = false;
        bool lastWDown = false;
        bool lastCDown = false;
        bool lastSpaceDown = false;

        private void UpdateInputs(TimeSpan timestamp)
        {
            TriggerInputs(timestamp);
        }

        private void TriggerInputs(TimeSpan timestamp)
        {
            if (Controller == null) return;
            var inputVecs = Controller.MoveIndicator;
            if (!lastADown && inputVecs.X < 0) DoA(timestamp);
            lastADown = inputVecs.X < 0;
            if (!lastSDown && inputVecs.Z > 0) DoS(timestamp);
            lastSDown = inputVecs.Z > 0;
            if (!lastDDown && inputVecs.X > 0) DoD(timestamp);
            lastDDown = inputVecs.X > 0;
            if (!lastWDown && inputVecs.Z < 0) DoW(timestamp);
            lastWDown = inputVecs.Z < 0;
            if (!lastCDown && inputVecs.Y < 0) DoC(timestamp);
            lastCDown = inputVecs.Y < 0;
            if (!lastSpaceDown && inputVecs.Y > 0) DoSpace(timestamp);
            lastSpaceDown = inputVecs.Y > 0;
        }

        void DoA(TimeSpan timestamp)
        {
            if (ActivePlugin != null) ActivePlugin.DoA(timestamp);
        }

        void DoS(TimeSpan timestamp)
        {
            //else if (CurrentUIMode == UIMode.Scan)
            //{
            //    TargetTracking_SelectionIndex = DeltaSelection(TargetTracking_SelectionIndex, TargetTracking_TargetList.Count, true);
            //}
            if (ActivePlugin != null) ActivePlugin.DoS(timestamp);
        }

        void DoD(TimeSpan timestamp)
        {
            //else if (CurrentUIMode == UIMode.Scan)
            //{
            //    if (TargetTracking_TargetList.Count > TargetTracking_SelectionIndex)
            //        TargetTracking_TrackID = TargetTracking_TargetList[TargetTracking_SelectionIndex].ID;
            //}
            if (ActivePlugin != null) ActivePlugin.DoD(timestamp);
        }

        void DoW(TimeSpan timestamp)
        {
            //else if (CurrentUIMode == UIMode.Scan)
            //{
            //    TargetTracking_SelectionIndex = DeltaSelection(TargetTracking_SelectionIndex, TargetTracking_TargetList.Count, false);
            //}
            if (ActivePlugin != null) ActivePlugin.DoW(timestamp);
        }

        void DoC(TimeSpan timestamp)
        {
            if (ActivePlugin != null) ActivePlugin.DoC(timestamp);
        }

        void DoSpace(TimeSpan timestamp)
        {
            //else if (CurrentUIMode == UIMode.Scan)
            //{
            //    TargetTracking_TrackID = -1;
            //    DoScan(timestamp);
            //}
            if (ActivePlugin != null) ActivePlugin.DoSpace(timestamp);
        }
        #endregion

        #region utils
        public void AppendPaddedLine(int TotalLength, string text, StringBuilder builder)
        {
            int length = text.Length;
            if (length > TotalLength)
                builder.AppendLine(text.Substring(0, TotalLength));
            else if (length < TotalLength)
                builder.Append(text).Append(' ', TotalLength - length).AppendLine();
            else
                builder.AppendLine(text);
        }

        public int CustomMod(int n, int d)
        {
            return (n % d + d) % d;
        }

        public int DeltaSelection(int current, int total, bool positive, int min = 0)
        {
            if (total == 0) return 0;
            int newCurrent = current + (positive ? 1 : -1);
            if (newCurrent >= total) newCurrent = 0;
            if (newCurrent <= -1) newCurrent = total - 1;
            return newCurrent;
        }

        #endregion

        #region Intel
        public void ReportIntel(IFleetIntelligence intel, TimeSpan timestamp)
        {
            IntelProvider.ReportFleetIntelligence(intel, timestamp);
        }

        #endregion

        #region updates
        private void UpdateUpdateFrequency()
        {
            UpdateFrequency = UpdateFrequency.Update10;
            if (ActiveLookingGlass != null) UpdateFrequency |= UpdateFrequency.Update1;
        }

        private void UpdatePlugins(TimeSpan timestamp)
        {
            foreach (var kvp in Plugins)
            {
                if (ActiveLookingGlass != null && kvp.Value == ActivePlugin) kvp.Value.UpdateHUD(timestamp);
                kvp.Value.UpdateState(timestamp);
            }
        }

        private void UpdateActiveLookingGlass()
        {
            ActiveLookingGlass = null;
            foreach (var lg in lookingGlasses)
            {
                if (lg.PrimaryCamera.IsActive)
                {
                    ActiveLookingGlass = lg;
                    lg.InterceptControls = true;
                }
                else
                {
                    lg.InterceptControls = false;
                }
            }

            Controller.ControlThrusters = ActiveLookingGlass == null;
            TerminalPropertiesHelper.SetValue(Controller, "ControlGyros", ActiveLookingGlass == null);
        }
        #endregion
    }


    public class LookingGlass : IControlIntercepter
    {
        #region IControlIntercepter
        public bool InterceptControls { get; set; }

        public IMyShipController Controller
        {
            get
            {
                return Network.Controller;
            }
        }
        #endregion

        public IMyTextPanel LeftHUD;
        public IMyTextPanel RightHUD;
        public IMyTextPanel MiddleHUD;
        public IMyCameraBlock PrimaryCamera;

        public LookingGlassNetworkSubsystem Network;

        List<IMyCameraBlock> secondaryCameras = new List<IMyCameraBlock>();

        string Tag;
        MyGridProgram Program;

        public LookingGlass(MyGridProgram program, string tag = "")
        {
            Tag = tag;
            Program = program;
            GetParts();
            LeftHUD.Alignment = TextAlignment.RIGHT;
        }

        void GetParts()
        {
            PrimaryCamera = null;
            LeftHUD = null;
            RightHUD = null;
            MiddleHUD = null;
            secondaryCameras.Clear();
            Program.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectParts);
        }


        private bool CollectParts(IMyTerminalBlock block)
        {
            if (!Program.Me.IsSameConstructAs(block)) return false;

            if (!block.CustomName.StartsWith(Tag)) return false;

            if (block is IMyTextPanel)
            {
                if (block.CustomName.Contains("[SN-SM]"))
                    MiddleHUD = (IMyTextPanel)block;
                if (block.CustomName.Contains("[SN-SL]"))
                    LeftHUD = (IMyTextPanel)block;
                if (block.CustomName.Contains("[SN-SR]"))
                    RightHUD = (IMyTextPanel)block;
            }

            if (block is IMyCameraBlock)
            {
                if (block.CustomName.Contains("[SN-C-S]"))
                    secondaryCameras.Add((IMyCameraBlock)block);
                else if (block.CustomName.Contains("[SN-C-P]"))
                    PrimaryCamera = (IMyCameraBlock)block;
            }

            return false;
        }

        #region Raycast
        private const int kScanDistance = 25000;
        int Lidar_CameraIndex = 0;
        public MyDetectedEntityInfo lastDetectedInfo;

        public void DoScan(TimeSpan timestamp)
        {
            DoScan(timestamp, Vector3D.Zero);
        }

        public bool DoScan(TimeSpan timestamp, Vector3D position)
        {
            IMyCameraBlock usingCamera = secondaryCameras[Lidar_CameraIndex];

            if (position == Vector3D.Zero)
                lastDetectedInfo = usingCamera.Raycast(usingCamera.AvailableScanRange);
            else if (!usingCamera.CanScan(position))
                return false;
            else
                lastDetectedInfo = usingCamera.Raycast(position);

            Lidar_CameraIndex += 1;
            if (Lidar_CameraIndex == secondaryCameras.Count) Lidar_CameraIndex = 0;

            if (lastDetectedInfo.Type == MyDetectedEntityType.Asteroid)
            {
                float radius = (float)(lastDetectedInfo.BoundingBox.Max - lastDetectedInfo.BoundingBox.Center).Length();
                var astr = new AsteroidIntel();
                astr.Radius = radius;
                astr.ID = lastDetectedInfo.EntityId;
                astr.Position = lastDetectedInfo.BoundingBox.Center;
                Network.ReportIntel(astr, timestamp);
            }
            else if ((lastDetectedInfo.Type == MyDetectedEntityType.LargeGrid || lastDetectedInfo.Type == MyDetectedEntityType.SmallGrid)
                && lastDetectedInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
            {
                var intelDict = Network.IntelProvider.GetFleetIntelligences(timestamp);
                var key = MyTuple.Create(IntelItemType.Enemy, lastDetectedInfo.EntityId);
                var TargetIntel = intelDict.ContainsKey(key) ? (EnemyShipIntel)intelDict[key] : new EnemyShipIntel();
                TargetIntel.FromDetectedInfo(lastDetectedInfo, timestamp + Network.IntelProvider.CanonicalTimeDiff, true);
                Network.ReportIntel(TargetIntel, timestamp);
            }

            return true;
        }

        //private void UpdateRaytracing()
        //{
        //    foreach (IMyCameraBlock camera in secondaryCameras)
        //    {
        //        camera.EnableRaycast = camera.AvailableScanRange < kScanDistance;
        //    }
        //}

        //private void DoTrack(TimeSpan timestamp)
        //{
        //    if (TargetTracking_TrackID == -1) return;
        //
        //    var intels = IntelProvider.GetFleetIntelligences(timestamp);
        //    var intelKey = MyTuple.Create(IntelItemType.Enemy, TargetTracking_TrackID);
        //
        //    if (!intels.ContainsKey(intelKey)) return;
        //
        //    var position = intels[intelKey].GetPositionFromCanonicalTime(timestamp + IntelProvider.CanonicalTimeDiff);
        //    var disp = position - primaryCamera.WorldMatrix.Translation;
        //
        //    if ((timestamp - TargetTracking_LastScanLocalTime).TotalSeconds < disp.Length() * 1.05 / (secondaryCameras.Count * 2000)) return;
        //
        //    if (DoScan(timestamp, primaryCamera.WorldMatrix.Translation + disp * 1.05))
        //    {
        //        TargetTracking_LastScanLocalTime = timestamp;
        //    }
        //}
        #endregion

        #region Display

        //private void DrawHUD(TimeSpan timestamp)
        //{
        //    UpdateCenterHUD(timestamp);
        //    UpdateLeftHUD(timestamp);
        //    UpdateRightHUD(timestamp);
        //}

        //private void UpdateCenterHUD(TimeSpan timestamp)
        //{
        //    if (MiddleHUD == null) return;
        //    using (var frame = MiddleHUD.DrawFrame())
        //    {
        //        MiddleHUD.ScriptBackgroundColor = new Color(1, 0, 0, 0);
        //
        //        MySprite crosshairs = GetCrosshair();
        //        frame.Add(crosshairs);
        //
        //        if (CurrentUIMode == UIMode.SelectWaypoint)
        //        {
        //            var distIndicator = MySprite.CreateText(CursorDist.ToString(), "Debug", Color.White, 0.5f);
        //            distIndicator.Position = new Vector2(0, 5) + MiddleHUD.TextureSize / 2f;
        //            frame.Add(distIndicator);
        //        }
        //
        //        foreach (IFleetIntelligence intel in IntelProvider.GetFleetIntelligences(timestamp).Values)
        //        {
        //            if (intel.IntelItemType == IntelItemType.Friendly && (CurrentUIMode == UIMode.Scan || AgentSelection_FriendlyAgents.Count == 0 || AgentSelection_FriendlyAgents.Count <= AgentSelection_CurrentIndex || intel != AgentSelection_FriendlyAgents[AgentSelection_CurrentIndex])) continue;
        //            FleetIntelItemToSprites(intel, timestamp, ref SpriteScratchpad);
        //        }
        //
        //        foreach (var spr in SpriteScratchpad)
        //        {
        //            frame.Add(spr);
        //        }
        //        SpriteScratchpad.Clear();
        //    }
        //}

        public const float kCameraToScreen = 1.06f;
        public const int kScreenSize = 512;

        public readonly Vector2 kMonospaceConstant = new Vector2(18.68108f, 28.8f);
        public readonly Vector2 kDebugConstant = new Vector2(18.68108f, 28.8f);

        const float kMinScale = 0.25f;
        const float kMaxScale = 0.5f;

        const float kMinDist = 1000;
        const float kMaxDist = 10000;

        List<MySprite> SpriteScratchpad = new List<MySprite>();

        [Flags]
        public enum IntelSpriteOptions
        {
            None = 0,
            EmphasizeWithBrackets = 1 << 0,
            EmphasizeWithDashes = 1 << 1,
            ShowDist = 1 << 2,
            ShowName = 1 << 3,
            Large = 1 << 4,
            Small = 1 << 5,
        }
        public void FleetIntelItemToSprites(IFleetIntelligence intel, TimeSpan localTime, Color color, ref List<MySprite> scratchpad, IntelSpriteOptions properties = IntelSpriteOptions.None)
        {
            var worldDirection = intel.GetPositionFromCanonicalTime(localTime + Network.IntelProvider.CanonicalTimeDiff) - PrimaryCamera.WorldMatrix.Translation;
            var bodyPosition = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(PrimaryCamera.WorldMatrix));
            var screenPosition = new Vector2(-1 * (float)(bodyPosition.X / bodyPosition.Z), (float)(bodyPosition.Y / bodyPosition.Z));

            if (bodyPosition.Dot(Vector3D.Forward) < 0) return;

            float dist = (float)bodyPosition.Length();
            float scale = kMaxScale;

            if (dist > kMaxDist) scale = kMinScale;
            else if (dist > kMinDist) scale = kMinScale + (kMaxScale - kMinScale) * (kMaxDist - dist) / (kMaxDist - kMinDist);
            if ((properties & IntelSpriteOptions.Large) != 0) scale *= 1.5f;
            else if ((properties & IntelSpriteOptions.Small) != 0) scale *= 0.5f;

            string indicatorText;
            bool brackets = (properties & IntelSpriteOptions.EmphasizeWithBrackets) != 0;
            bool dashes = (properties & IntelSpriteOptions.EmphasizeWithDashes) != 0;
            if (brackets && dashes) indicatorText = "-[><]-";
            else if (brackets) indicatorText = "[><]";
            else if (dashes) indicatorText = "-><-";
            else indicatorText = "><";

            var indicator = MySprite.CreateText(indicatorText, "Monospace", color, scale, TextAlignment.CENTER);
            var v = ((screenPosition * kCameraToScreen) + new Vector2(0.5f, 0.5f)) * kScreenSize;
            v.X = Math.Max(30, Math.Min(kScreenSize - 30, v.X));
            v.Y = Math.Max(30, Math.Min(kScreenSize - 30, v.Y));
            v.Y -= scale * (kMonospaceConstant.Y + 2) / 2;
            indicator.Position = v;
            scratchpad.Add(indicator);
            v.Y += kMonospaceConstant.Y * scale + 0.2f;

            if ((properties & IntelSpriteOptions.ShowDist) != 0)
            {
                var distSprite = MySprite.CreateText($"{((int)dist).ToString()} m", "Debug", new Color(1, 1, 1, 0.5f), 0.4f, TextAlignment.CENTER);
                distSprite.Position = v;
                scratchpad.Add(distSprite);
                v.Y += kDebugConstant.Y * 0.4f + 0.1f;
            }

            if ((properties & IntelSpriteOptions.ShowName) != 0)
            {
                var nameSprite = MySprite.CreateText(intel.DisplayName, "Debug", new Color(1, 1, 1, 0.5f), 0.4f, TextAlignment.CENTER);
                nameSprite.Position = v;
                scratchpad.Add(nameSprite);
                v.Y += kDebugConstant.Y * 0.4f + 0.1f;
            }
        }

        public MySprite GetCrosshair()
        {
            var crosshairs = new MySprite(SpriteType.TEXTURE, "Cross", size: new Vector2(10f, 10f), color: new Color(1, 1, 1, 0.1f));
            crosshairs.Position = new Vector2(0, -2) + MiddleHUD.TextureSize / 2f;
            return crosshairs;
        }


        //private void DrawScanUI(TimeSpan timestamp)
        //{
        //    LeftHUD.FontSize = 0.55f;
        //    LeftHUD.TextPadding = 9;
        //    int kRowLength = 19;
        //
        //    LeftHUDBuilder.AppendLine("====== LIDAR ======");
        //    LeftHUDBuilder.AppendLine();
        //
        //    if (secondaryCameras.Count == 0)
        //    {
        //        LeftHUDBuilder.AppendLine("=== UNAVAILABLE ===");
        //        return;
        //    }
        //
        //    AppendPaddedLine(kRowLength, "SCANNERS:", LeftHUDBuilder);
        //
        //    LeftHUDBuilder.AppendLine();
        //
        //    for (int i = 0; i < 8; i++)
        //    {
        //        if (i < secondaryCameras.Count)
        //        {
        //            LeftHUDBuilder.Append(i == Lidar_CameraIndex ? "> " : "  ");
        //            LeftHUDBuilder.Append((i + 1).ToString()).Append(": ");
        //
        //            if (secondaryCameras[i].IsWorking)
        //            {
        //                if (secondaryCameras[i].AvailableScanRange >= kScanDistance)
        //                {
        //                    AppendPaddedLine(kRowLength - 5, "READY", LeftHUDBuilder);
        //                }
        //                else
        //                {
        //                    AppendPaddedLine(kRowLength - 5, "CHARGING", LeftHUDBuilder);
        //                }
        //
        //                int p = (int)(secondaryCameras[i].AvailableScanRange * 10 / kScanDistance);
        //                LeftHUDBuilder.Append('[').Append('=', p).Append(' ', Math.Max(0, 10 - p)).Append(string.Format("] {0,4:0.0}", secondaryCameras[i].AvailableScanRange / 1000)).AppendLine("km");
        //            }
        //            else
        //            {
        //                AppendPaddedLine(kRowLength - 5, "UNAVAILABLE", LeftHUDBuilder);
        //                LeftHUDBuilder.AppendLine();
        //            }
        //        }
        //        else
        //        {
        //            LeftHUDBuilder.AppendLine();
        //            LeftHUDBuilder.AppendLine();
        //        }
        //    }
        //
        //    LeftHUDBuilder.AppendLine();
        //    LeftHUDBuilder.AppendLine("===================");
        //
        //    if (secondaryCameras[Lidar_CameraIndex].IsWorking)
        //    {
        //        AppendPaddedLine(kRowLength, "STATUS: AVAILABLE", LeftHUDBuilder);
        //        int p = (int)(secondaryCameras[Lidar_CameraIndex].AvailableScanRange * 10 / kScanDistance);
        //        LeftHUDBuilder.Append('[').Append('=', p).Append(' ', Math.Max(0, 10 - p)).Append(string.Format("] {0,4:0.0}", secondaryCameras[Lidar_CameraIndex].AvailableScanRange / 1000)).AppendLine("km");
        //        AppendPaddedLine(kRowLength, "[SPACE] SCAN", LeftHUDBuilder);
        //        AppendPaddedLine(kRowLength, lastDetectedInfo.Type.ToString(), LeftHUDBuilder);
        //    }
        //    else
        //    {
        //        AppendPaddedLine(kRowLength, "STATUS: UNAVAILABLE", LeftHUDBuilder);
        //        AppendPaddedLine(kRowLength, "[SPACE] CYCLE", LeftHUDBuilder);
        //    }
        //}

        //List<EnemyShipIntel> TargetTracking_TargetList = new List<EnemyShipIntel>();
        //int TargetTracking_SelectionIndex = 0;
        //long TargetTracking_TrackID = -1;
        //
        //TimeSpan TargetTracking_LastScanLocalTime;
        //
        //private void DrawTrackingUI(TimeSpan timestamp)
        //{
        //    RightHUD.FontSize = 0.55f;
        //    RightHUD.TextPadding = 9;
        //
        //    RightHUDBuilder.AppendLine("= TARGET TRACKING =");
        //
        //    RightHUDBuilder.AppendLine();
        //
        //    var intels = IntelProvider.GetFleetIntelligences(timestamp);
        //    var canonicalTime = timestamp + IntelProvider.CanonicalTimeDiff;
        //    TargetTracking_TargetList.Clear();
        //
        //    bool hasTarget = false;
        //
        //    foreach (var intel in intels)
        //    {
        //        if (intel.Key.Item1 == IntelItemType.Enemy)
        //        {
        //            if (!hasTarget && intel.Key.Item2 == TargetTracking_TrackID) hasTarget = true;
        //            TargetTracking_TargetList.Add((EnemyShipIntel)intel.Value);
        //        }
        //    }
        //
        //    if (!hasTarget) TargetTracking_TrackID = -1;
        //    if (TargetTracking_TargetList.Count <= TargetTracking_SelectionIndex) TargetTracking_SelectionIndex = 0;
        //
        //
        //    if (TargetTracking_TargetList.Count == 0)
        //    {
        //        RightHUDBuilder.AppendLine("NO TARGETS");
        //        return;
        //    }
        //
        //    RightHUDBuilder.AppendLine("WS SELECT | D TRACK");
        //    RightHUDBuilder.AppendLine();
        //
        //    for (int i = 0; i < 20; i++)
        //    {
        //        if (i < TargetTracking_TargetList.Count)
        //        {
        //            RightHUDBuilder.Append(TargetTracking_TargetList[i].ID == TargetTracking_TrackID ? '-' : ' ');
        //            RightHUDBuilder.Append(i == TargetTracking_SelectionIndex ? '>' : ' ');
        //            AppendPaddedLine(17, TargetTracking_TargetList[i].DisplayName, RightHUDBuilder);
        //        }
        //        else
        //        {
        //            RightHUDBuilder.AppendLine();
        //        }
        //    }
        //
        //    RightHUDBuilder.AppendLine();
        //
        //}

        #endregion
    }

    public interface ILookingGlassPlugin
    {
        void DoA(TimeSpan localTime);
        void DoS(TimeSpan localTime);
        void DoD(TimeSpan localTime);
        void DoW(TimeSpan localTime);
        void DoC(TimeSpan localTime);
        void DoSpace(TimeSpan localTime);

        void UpdateHUD(TimeSpan localTime);
        void UpdateState(TimeSpan localTime);

        void Setup();

        LookingGlassNetworkSubsystem Host { get; set; }
    }

    public class LookingGlassPlugin_Command : ILookingGlassPlugin
    {
        #region ILookingGlassPlugin
        public LookingGlassNetworkSubsystem Host { get; set; }
        public void DoA(TimeSpan localTime)
        {
            if (CurrentUIMode == UIMode.SelectAgent)
            {
                AgentSelection_CurrentClass = AgentClassAdd(AgentSelection_CurrentClass, -1);
                AgentSelection_CurrentIndex = 0;
            }
            else if (CurrentUIMode == UIMode.SelectTarget)
            {
                TargetSelection_TaskTypesIndex = Host.DeltaSelection(TargetSelection_TaskTypesIndex, TargetSelection_TaskTypes.Count, false);
            }
        }

        public void DoS(TimeSpan localTime)
        {
            if (CurrentUIMode == UIMode.SelectAgent)
            {
                AgentSelection_CurrentIndex = Host.DeltaSelection(AgentSelection_CurrentIndex, AgentSelection_FriendlyAgents.Count, true);
            }
            else if (CurrentUIMode == UIMode.SelectTarget)
            {
                TargetSelection_TargetIndex = Host.DeltaSelection(TargetSelection_TargetIndex, TargetSelection_Targets.Count + TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]].Count(), true);
            }
            else if (CurrentUIMode == UIMode.SelectWaypoint)
            {
                CursorDist -= 200;
            }
        }

        public void DoD(TimeSpan localTime)
        {
            if (CurrentUIMode == UIMode.SelectAgent)
            {
                AgentSelection_CurrentClass = AgentClassAdd(AgentSelection_CurrentClass, 1);
                AgentSelection_CurrentIndex = 0;
            }
            else if (CurrentUIMode == UIMode.SelectTarget)
            {
                TargetSelection_TaskTypesIndex = Host.DeltaSelection(TargetSelection_TaskTypesIndex, TargetSelection_TaskTypes.Count, true);
            }
        }
        public void DoW(TimeSpan localTime)
        {
            if (CurrentUIMode == UIMode.SelectAgent)
            {
                AgentSelection_CurrentIndex = Host.DeltaSelection(AgentSelection_CurrentIndex, AgentSelection_FriendlyAgents.Count, false);
            }
            else if (CurrentUIMode == UIMode.SelectTarget)
            {
                TargetSelection_TargetIndex = Host.DeltaSelection(TargetSelection_TargetIndex, TargetSelection_Targets.Count + TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]].Count(), false);
            }
            else if (CurrentUIMode == UIMode.SelectWaypoint)
            {
                CursorDist += 200;
            }
        }

        public void DoC(TimeSpan localTime)
        {
            if (CurrentUIMode == UIMode.SelectTarget)
            {
                CurrentUIMode = UIMode.SelectAgent;
            }
            else if(CurrentUIMode == UIMode.SelectWaypoint || CurrentUIMode == UIMode.Designate)
            {
                CurrentUIMode = UIMode.SelectTarget;
            }
        }

        public void DoSpace(TimeSpan localTime)
        {
            if (CurrentUIMode == UIMode.SelectAgent)
            {
                CurrentUIMode = UIMode.SelectTarget;
            }
            else if(CurrentUIMode == UIMode.SelectTarget)
            {
                if (TargetSelection_TargetIndex < TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]].Count())
                {
                    // Special handling
                    string SpecialCommand = TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]][TargetSelection_TargetIndex];
                    if (SpecialCommand == "CURSOR")
                    {
                        CurrentUIMode = UIMode.SelectWaypoint;
                    }
                    if (SpecialCommand == "HOME")
                    {
                        SendCommand(MyTuple.Create(IntelItemType.NONE, (long)0), localTime);
                        CurrentUIMode = UIMode.SelectAgent;
                    }
                    if (SpecialCommand == "DESIGNATE")
                    {
                        CurrentUIMode = UIMode.Designate;
                    }
                }
                else if (TargetSelection_TargetIndex < TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]].Count() + TargetSelection_Targets.Count())
                {
                    SendCommand(TargetSelection_Targets[TargetSelection_TargetIndex - TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]].Count()], localTime);
                    CurrentUIMode = UIMode.SelectAgent;
                }
            }
            else if (CurrentUIMode == UIMode.SelectWaypoint)
            {
                Waypoint w = GetWaypoint();
                w.MaxSpeed = 100;
                Host.ReportIntel(w, localTime);
                SendCommand(w, localTime);
            }
            else if (CurrentUIMode == UIMode.Designate)
            {
                Host.ActiveLookingGlass.DoScan(localTime);
                if (!Host.ActiveLookingGlass.lastDetectedInfo.IsEmpty() && Host.ActiveLookingGlass.lastDetectedInfo.Type == MyDetectedEntityType.Asteroid)
                {
                    var w = new Waypoint();
                    w.Position = (Vector3D)Host.ActiveLookingGlass.lastDetectedInfo.HitPosition;
                    Host.ReportIntel(w, localTime);
                    SendCommand(w, localTime);
                }
                CurrentUIMode = UIMode.SelectAgent;
            }
        }


        public void Setup()
        {
        }

        public void UpdateHUD(TimeSpan localTime)
        {
            DrawTargetSelectionUI(localTime);
            DrawMiddleHUD(localTime);
            DrawAgentSelectionUI(localTime);
        }

        public void UpdateState(TimeSpan localTime)
        {
        }
        #endregion

        StringBuilder Builder = new StringBuilder();

        enum UIMode
        {
            SelectAgent,
            SelectTarget,
            SelectWaypoint,
            Designate,
        }
        UIMode CurrentUIMode = UIMode.SelectAgent;

        List<MySprite> SpriteScratchpad = new List<MySprite>();

        public readonly Color kFocusedColor = new Color(0.5f, 0.5f, 1f);
        public readonly Color kUnfocusedColor = new Color(0.2f, 0.2f, 0.5f, 0.5f);

        #region SelectAgent
        private void DrawAgentSelectionUI(TimeSpan timestamp)
        {
            Builder.Clear();
            Host.ActiveLookingGlass.LeftHUD.FontSize = 0.55f;
            Host.ActiveLookingGlass.LeftHUD.TextPadding = 9;
            Host.ActiveLookingGlass.LeftHUD.FontColor = CurrentUIMode == UIMode.SelectAgent ? kFocusedColor : kUnfocusedColor;

            int kRowLength = 19;
            int kMenuRows = 12;

            Builder.AppendLine("===== COMMAND =====");
            Builder.AppendLine();
            Builder.Append(AgentClassTags[AgentClassAdd(AgentSelection_CurrentClass, -1)]).Append("    [").Append(AgentClassTags[AgentSelection_CurrentClass]).Append("]    ").AppendLine(AgentClassTags[AgentClassAdd(AgentSelection_CurrentClass, +1)]);
            if (CurrentUIMode == UIMode.SelectAgent) Builder.AppendLine("[<A]           [D>]");
            else Builder.AppendLine();

            Builder.AppendLine();

            AgentSelection_FriendlyAgents.Clear();

            foreach (IFleetIntelligence intel in Host.IntelProvider.GetFleetIntelligences(timestamp).Values)
            {
                if (intel is FriendlyShipIntel && ((FriendlyShipIntel)intel).AgentClass == AgentSelection_CurrentClass)
                    AgentSelection_FriendlyAgents.Add((FriendlyShipIntel)intel);
            }
            AgentSelection_FriendlyAgents.Sort(FleetIntelligenceUtil.CompareName);

            for (int i = 0; i < kMenuRows; i++)
            {
                if (i < AgentSelection_FriendlyAgents.Count)
                {
                    var intel = AgentSelection_FriendlyAgents[i];
                    if (i == AgentSelection_CurrentIndex) Builder.Append(">> ");
                    else Builder.Append("   ");

                    Host.AppendPaddedLine(kRowLength - 3, intel.DisplayName, Builder);
                }
                else
                {
                    Builder.AppendLine();
                }
            }

            Builder.AppendLine("==== SELECTION ====");

            if (AgentSelection_CurrentIndex < AgentSelection_FriendlyAgents.Count)
            {
                Host.AppendPaddedLine(kRowLength, AgentSelection_FriendlyAgents[AgentSelection_CurrentIndex].DisplayName, Builder);
                if (CurrentUIMode == UIMode.SelectAgent) Host.AppendPaddedLine(kRowLength, "[SPACE] SELECT TGT", Builder);
            }
            else
            {
                Host.AppendPaddedLine(kRowLength, "NONE SELECTED", Builder);
            }
            Host.ActiveLookingGlass.LeftHUD.WriteText(Builder.ToString());
        }

        AgentClass AgentSelection_CurrentClass = AgentClass.Drone;
        int AgentSelection_CurrentIndex = 0;
        List<FriendlyShipIntel> AgentSelection_FriendlyAgents = new List<FriendlyShipIntel>();

        Dictionary<AgentClass, string> AgentClassTags = new Dictionary<AgentClass, string>
        {
            { AgentClass.None, "SLF" },
            { AgentClass.Drone, "DRN" },
            { AgentClass.Fighter, "FTR" },
            { AgentClass.Bomber, "BMR" },
            { AgentClass.Miner, "MNR" },
            { AgentClass.Carrier, "CVS" },
        };

        AgentClass AgentClassAdd(AgentClass agentClass, int places = 1)
        {
            return (AgentClass)Host.CustomMod((int)agentClass + places, (int)AgentClass.Last);
        }
        #endregion

        #region SelectTarget
        Dictionary<TaskType, string> TaskTypeTags = new Dictionary<TaskType, string>
        {
            { TaskType.None, "N/A" },
            { TaskType.Move, "MOV" },
            { TaskType.SmartMove, "SMV" },
            { TaskType.Attack, "ATK" },
            { TaskType.Dock, "DOK" },
            { TaskType.SetHome, "HOM" },
            { TaskType.Mine, "MNE" }
        };

        Dictionary<TaskType, IntelItemType> TaskTypeToTargetTypes = new Dictionary<TaskType, IntelItemType>
        {
            { TaskType.None, IntelItemType.NONE},
            { TaskType.Move, IntelItemType.Waypoint},
            { TaskType.SmartMove, IntelItemType.Waypoint },
            { TaskType.Attack, IntelItemType.Enemy | IntelItemType.Waypoint },
            { TaskType.Dock, IntelItemType.Dock },
            { TaskType.SetHome, IntelItemType.Dock },
            { TaskType.Mine, IntelItemType.Waypoint }
        };

        Dictionary<TaskType, string[]> TaskTypeToSpecialTargets = new Dictionary<TaskType, string[]>
        {
            { TaskType.None, new string[0]},
            { TaskType.Move, new string[1] { "CURSOR" }},
            { TaskType.SmartMove, new string[1] { "CURSOR" }},
            { TaskType.Attack, new string[2] { "CURSOR", "NEAREST" }},
            { TaskType.Dock, new string[2] { "NEAREST", "HOME" }},
            { TaskType.SetHome, new string[0]},
            { TaskType.Mine, new string[1] { "DESIGNATE" }}
        };

        List<TaskType> TargetSelection_TaskTypes = new List<TaskType>();
        int TargetSelection_TaskTypesIndex = 0;
        List<IFleetIntelligence> TargetSelection_Targets = new List<IFleetIntelligence>();
        int TargetSelection_TargetIndex;

        private void DrawTargetSelectionUI(TimeSpan timestamp)
        {
            Builder.Clear();

            Host.ActiveLookingGlass.RightHUD.FontSize = 0.55f;
            Host.ActiveLookingGlass.RightHUD.TextPadding = 9;
            Host.ActiveLookingGlass.RightHUD.FontColor = CurrentUIMode == UIMode.SelectTarget ? kFocusedColor : kUnfocusedColor;

            Builder.AppendLine("=== SELECT TASK ===");

            Builder.AppendLine();

            if (AgentSelection_CurrentIndex >= AgentSelection_FriendlyAgents.Count) return;

            var Agent = AgentSelection_FriendlyAgents[AgentSelection_CurrentIndex];
            TargetSelection_TaskTypes.Clear();

            for (int i = 0; i < 30; i++)
            {
                if (((TaskType)(1 << i) & Agent.AcceptedTaskTypes) != 0)
                    TargetSelection_TaskTypes.Add((TaskType)(1 << i));
            }

            if (TargetSelection_TaskTypes.Count == 0) return;

            if (TargetSelection_TaskTypesIndex >= TargetSelection_TaskTypes.Count) TargetSelection_TaskTypesIndex = 0;

            Builder.Append(TaskTypeTags[TargetSelection_TaskTypes[Host.CustomMod(TargetSelection_TaskTypesIndex - 1, TargetSelection_TaskTypes.Count)]]).
                Append("    [").Append(TaskTypeTags[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]]).Append("]    ").
                AppendLine(TaskTypeTags[TargetSelection_TaskTypes[Host.CustomMod(TargetSelection_TaskTypesIndex + 1, TargetSelection_TaskTypes.Count)]]);

            if (CurrentUIMode == UIMode.SelectTarget) Builder.AppendLine("[<A]           [D>]");
            else Builder.AppendLine();
            Builder.AppendLine();

            TargetSelection_Targets.Clear();
            foreach (IFleetIntelligence intel in Host.IntelProvider.GetFleetIntelligences(timestamp).Values)
            {
                if ((intel.IntelItemType & TaskTypeToTargetTypes[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]]) != 0)
                    TargetSelection_Targets.Add(intel);
            }

            TargetSelection_Targets.Sort(FleetIntelligenceUtil.CompareName);

            int kMenuRows = 16;
            int kRowLength = 19;
            int specialCount = TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]].Count();
            for (int i = 0; i < kMenuRows; i++)
            {
                if (i < specialCount)
                {
                    if (i == TargetSelection_TargetIndex) Builder.Append(">> ");
                    else Builder.Append("   ");
                    Host.AppendPaddedLine(kRowLength - 3, TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]][i], Builder);
                }
                else if (specialCount < i && i < TargetSelection_Targets.Count + specialCount + 1)
                {
                    if (i == TargetSelection_TargetIndex + 1) Builder.Append(">> ");
                    else Builder.Append("   ");
                    var intel = TargetSelection_Targets[i - specialCount - 1];

                    if (intel is DockIntel)
                    {
                        var dockIntel = (DockIntel)intel;
                        Builder.Append(dockIntel.OwnerID != -1 ? ((dockIntel.Status & HangarStatus.Reserved) != 0 ? "[R]" : "[C]") : "[ ]");
                        Host.AppendPaddedLine(kRowLength - 6, intel.DisplayName, Builder);
                    }
                    else
                    {
                        Host.AppendPaddedLine(kRowLength - 3, intel.DisplayName, Builder);
                    }

                }
                else
                {
                    Builder.AppendLine();
                }
            }

            Builder.AppendLine();

            Builder.AppendLine("==== SELECTION ====");

            if (TargetSelection_TargetIndex < specialCount)
            {
                Host.AppendPaddedLine(kRowLength, TaskTypeToSpecialTargets[TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex]][TargetSelection_TargetIndex], Builder);
                if (CurrentUIMode == UIMode.SelectTarget)
                {
                    Host.AppendPaddedLine(kRowLength, "[SPACE] SELECT", Builder);
                    Host.AppendPaddedLine(kRowLength, "[C] CANCLE CMD", Builder);
                }
            }
            else if (specialCount <= TargetSelection_TargetIndex && TargetSelection_TargetIndex < TargetSelection_Targets.Count + specialCount)
            {
                Host.AppendPaddedLine(kRowLength, TargetSelection_Targets[TargetSelection_TargetIndex - specialCount].ID.ToString(), Builder);
                if (CurrentUIMode == UIMode.SelectTarget)
                {
                    Host.AppendPaddedLine(kRowLength, "[SPACE] SEND CMD", Builder);
                    Host.AppendPaddedLine(kRowLength, "[C] CANCLE CMD", Builder);
                }
            }
            else
            {
                Host.AppendPaddedLine(kRowLength, "NONE SELECTED", Builder);
            }

            Host.ActiveLookingGlass.RightHUD.WriteText(Builder.ToString());
        }
        #endregion

        #region MiddleHUD
        void DrawMiddleHUD(TimeSpan localTime)
        {
            if (Host.ActiveLookingGlass.MiddleHUD == null) return;
            using (var frame = Host.ActiveLookingGlass.MiddleHUD.DrawFrame())
            {
                SpriteScratchpad.Clear();
                Host.ActiveLookingGlass.MiddleHUD.ScriptBackgroundColor = new Color(1, 0, 0, 0);
            
                MySprite crosshairs = Host.ActiveLookingGlass.GetCrosshair();
                frame.Add(crosshairs);
            
                if (CurrentUIMode == UIMode.SelectWaypoint)
                {
                    var distIndicator = MySprite.CreateText(CursorDist.ToString(), "Debug", Color.White, 0.5f);
                    distIndicator.Position = new Vector2(0, 5) + Host.ActiveLookingGlass.MiddleHUD.TextureSize / 2f;
                    frame.Add(distIndicator);
                }
            
                foreach (IFleetIntelligence intel in Host.IntelProvider.GetFleetIntelligences(localTime).Values)
                {
                    if (intel.IntelItemType == IntelItemType.Friendly)
                    {
                        if (AgentSelection_FriendlyAgents.Count > 0 && AgentSelection_FriendlyAgents.Count > AgentSelection_CurrentIndex && intel == AgentSelection_FriendlyAgents[AgentSelection_CurrentIndex])
                            Host.ActiveLookingGlass.FleetIntelItemToSprites(intel, localTime, Color.AliceBlue, ref SpriteScratchpad, LookingGlass.IntelSpriteOptions.ShowName | LookingGlass.IntelSpriteOptions.ShowDist | LookingGlass.IntelSpriteOptions.EmphasizeWithDashes);
                        else
                            Host.ActiveLookingGlass.FleetIntelItemToSprites(intel, localTime, Color.AliceBlue, ref SpriteScratchpad, LookingGlass.IntelSpriteOptions.Small);
                    }
                    else if (intel.IntelItemType == IntelItemType.Enemy)
                    {
                        Host.ActiveLookingGlass.FleetIntelItemToSprites(intel, localTime, Color.LightPink, ref SpriteScratchpad, LookingGlass.IntelSpriteOptions.Small);
                    }
                }
            
                foreach (var spr in SpriteScratchpad)
                {
                    frame.Add(spr);
                }
            }
        }
        #endregion

        #region util

        void SendCommand(IFleetIntelligence target, TimeSpan timestamp)
        {
            SendCommand(MyTuple.Create(target.IntelItemType, target.ID), timestamp);
        }
        void SendCommand(MyTuple<IntelItemType, long> targetKey, TimeSpan timestamp)
        {
            FriendlyShipIntel agent = AgentSelection_FriendlyAgents[AgentSelection_CurrentIndex];
            TaskType taskType = TargetSelection_TaskTypes[TargetSelection_TaskTypesIndex];

            Host.IntelProvider.ReportCommand(agent, taskType, targetKey, timestamp);

            CurrentUIMode = UIMode.SelectAgent;
        }
        #endregion

        #region Waypoint Designation
        int CursorDist = 1000;
        Waypoint GetWaypoint()
        {
            var w = new Waypoint();
            w.Position = Vector3D.Transform(Vector3D.Forward * CursorDist, Host.ActiveLookingGlass.PrimaryCamera.WorldMatrix);
        
            return w;
        }
        #endregion
    }
}
