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
    public class TorpedoSubsystem : ISubsystem
    {
        #region ISubsystem
        public UpdateFrequency UpdateFrequency { get; set; }

        public void Command(TimeSpan timestamp, string command, object argument)
        {
        }

        public void DeserializeSubsystem(string serialized)
        {
        }

        public string GetStatus()
        {
            debugBuilder.Clear();
            if (TorpedoTubes[1] != null)
            {
                TorpedoTubes[1].GetTorpedo();
                debugBuilder.AppendLine($"{TorpedoTubes[1].LoadedTorpedo.thursters.Count()} THRUSTERS");
                debugBuilder.AppendLine($"{TorpedoTubes[1].LoadedTorpedo.warheads.Count()} WARHEADS");
                //debugBuilder.AppendLine($"{TorpedoTubes[1].LoadedTorpedo.camera.CustomName} CAMERA");
                //debugBuilder.AppendLine($"{TorpedoTubes[1].LoadedTorpedo.gyro.CustomName} GYRO");
                //debugBuilder.AppendLine($"{TorpedoTubes[1].Projectors.Count} PROJECTORS");
                //debugBuilder.AppendLine($"{TorpedoTubes[1].Welders.Count} WELDERS");
                //debugBuilder.AppendLine($"{TorpedoTubes[1].Release.CustomName} RELEASE");
            }

            return debugBuilder.ToString();
        }

        public string SerializeSubsystem()
        {
            return string.Empty;
        }

        public void Setup(MyGridProgram program, string name)
        {
            Program = program;
            GetParts();
        }

        public void Update(TimeSpan timestamp, UpdateFrequency updateFlags)
        {
        }
        #endregion

        public TorpedoSubsystem(IIntelProvider intelProvider)
        {
            UpdateFrequency = UpdateFrequency.Update10;
            IntelProvider = intelProvider;
        }

        TorpedoTube[] TorpedoTubes = new TorpedoTube[16];

        HashSet<Torpedo> Torpedoes = new HashSet<Torpedo>();

        MyGridProgram Program;
        IIntelProvider IntelProvider;

        StringBuilder debugBuilder = new StringBuilder();

        void GetParts()
        {
            for (int i = 0; i < TorpedoTubes.Count(); i++)
            {
                TorpedoTubes[i] = null;
            }

            Program.GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectParts);
        }

        private bool CollectParts(IMyTerminalBlock block)
        {
            if (!Program.Me.IsSameConstructAs(block)) return false; // Allow subgrid
            if (!block.CustomName.StartsWith("[TRP")) return false;

            var indexTagEnd = block.CustomName.IndexOf(']');
            if (indexTagEnd == -1) return false;

            var numString = block.CustomName.Substring(4, indexTagEnd - 4);

            int index;
            if (!int.TryParse(numString, out index)) return false;
            if (TorpedoTubes[index] == null) TorpedoTubes[index] = new TorpedoTube(Program, this);
            TorpedoTubes[index].AddPart(block);

            return false;
        }

        pr
    }

    // This is a refhax torpedo
    public class Torpedo
    {
        public IMyGyro gyro;
        public HashSet<IMyWarhead> warheads = new HashSet<IMyWarhead>();
        public HashSet<IMyThrust> thursters = new HashSet<IMyThrust>();
        public IMyCameraBlock camera; // The camera is the reference for the torpedo
        public string type; // HE, CLST, etc

        GyroControl gyroControl;

        PDController yawController = new PDController(DEF_PD_P_GAIN, DEF_PD_D_GAIN, 10);
        PDController pitchController = new PDController(DEF_PD_P_GAIN, DEF_PD_D_GAIN, 10);

        TimeSpan launchTime = TimeSpan.Zero;

        public void AddPart(IMyTerminalBlock block)
        {
            if (block is IMyGyro) gyro = (IMyGyro)block;
            if (block is IMyCameraBlock) camera = (IMyCameraBlock)block;
            if (block is IMyThrust) thursters.Add((IMyThrust)block);
            if (block is IMyWarhead) warheads.Add((IMyWarhead)block);
        }

        public void Init(TimeSpan CanonicalTime)
        {
            List<IMyGyro> gyros = new List<IMyGyro>();
            gyros.Add(gyro);
            gyroControl = new GyroControl(gyros);
            var refWorldMatrix = camera.WorldMatrix;
            gyroControl.Init(ref refWorldMatrix);

            foreach (var thruster in thursters) thruster.ThrustOverridePercentage = 1;

            launchTime = CanonicalTime;
        }

        public void Update(EnemyShipIntel Target, TimeSpan CanonicalTime)
        {

        }

        void AimAtTarget(Vector3D TargetVector)
        {
            //---------- Activate Gyroscopes To Turn Towards Target ----------

            double absX = Math.Abs(TargetVector.X);
            double absY = Math.Abs(TargetVector.Y);
            double absZ = Math.Abs(TargetVector.Z);

            double yawInput, pitchInput;
            if (absZ < 0.00001)
            {
                yawInput = pitchInput = MathHelperD.PiOver2;
            }
            else
            {
                bool flipYaw = absX > absZ;
                bool flipPitch = absY > absZ;

                yawInput = FastAT(Math.Max(flipYaw ? (absZ / absX) : (absX / absZ), 0.00001));
                pitchInput = FastAT(Math.Max(flipPitch ? (absZ / absY) : (absY / absZ), 0.00001));

                if (flipYaw) yawInput = MathHelperD.PiOver2 - yawInput;
                if (flipPitch) pitchInput = MathHelperD.PiOver2 - pitchInput;

                if (TargetVector.Z > 0)
                {
                    yawInput = (Math.PI - yawInput);
                    pitchInput = (Math.PI - pitchInput);
                }
            }

            //---------- PID Controller Adjustment ----------

            if (double.IsNaN(yawInput)) yawInput = 0;
            if (double.IsNaN(pitchInput)) pitchInput = 0;

            yawInput *= GetSign(TargetVector.X);
            pitchInput *= GetSign(TargetVector.Y);

            yawInput = yawController.Filter(yawInput, 2);
            pitchInput = pitchController.Filter(pitchInput, 2);

            if (Math.Abs(yawInput) + Math.Abs(pitchInput) > DEF_PD_AIM_LIMIT)
            {
                double adjust = DEF_PD_AIM_LIMIT / (Math.Abs(yawInput) + Math.Abs(pitchInput));
                yawInput *= adjust;
                pitchInput *= adjust;
            }

            //---------- Set Gyroscope Parameters ----------

            gyroControl.SetGyroYaw((float)yawInput);
            gyroControl.SetGyroPitch((float)pitchInput);
        }

        const double DEF_PD_P_GAIN = 20;
        const double DEF_PD_D_GAIN = 10;
        const double DEF_PD_AIM_LIMIT = 6.3;

        double FastAT(double x)
        {
            //Removed Math.Abs() since x is always positive in this script
            return 0.785375 * x - x * (x - 1.0) * (0.2447 + 0.0663 * x);
        }

        double GetSign(double value)
        {
            return (value < 0 ? -1 : 1);
        }

    }

    public class GyroControl
    {
        Action<IMyGyro, float>[] profiles =
        {
            (g, v) => { g.Yaw = -v; },
            (g, v) => { g.Yaw = v; },
            (g, v) => { g.Pitch = -v; },
            (g, v) => { g.Pitch = v; },
            (g, v) => { g.Roll = -v; },
            (g, v) => { g.Roll = v; }
        };

        List<IMyGyro> gyros;
        public List<IMyGyro> Gyroscopes { get { return gyros; } }

        byte[] gyroYaw;
        byte[] gyroPitch;
        byte[] gyroRoll;

        int activeGyro = 0;

        public GyroControl(List<IMyGyro> newGyros)
        {
            gyros = newGyros;
        }

        public void Init(ref MatrixD refWorldMatrix)
        {
            if (gyros == null)
            {
                gyros = new List<IMyGyro>();
            }

            gyroYaw = new byte[gyros.Count];
            gyroPitch = new byte[gyros.Count];
            gyroRoll = new byte[gyros.Count];

            for (int i = 0; i < gyros.Count; i++)
            {
                gyroYaw[i] = SetRelativeDirection(gyros[i].WorldMatrix.GetClosestDirection(refWorldMatrix.Up));
                gyroPitch[i] = SetRelativeDirection(gyros[i].WorldMatrix.GetClosestDirection(refWorldMatrix.Left));
                gyroRoll[i] = SetRelativeDirection(gyros[i].WorldMatrix.GetClosestDirection(refWorldMatrix.Forward));
            }

            activeGyro = 0;
        }

        public byte SetRelativeDirection(Base6Directions.Direction dir)
        {
            switch (dir)
            {
                case Base6Directions.Direction.Up:
                    return 1;
                case Base6Directions.Direction.Down:
                    return 0;
                case Base6Directions.Direction.Left:
                    return 2;
                case Base6Directions.Direction.Right:
                    return 3;
                case Base6Directions.Direction.Forward:
                    return 4;
                case Base6Directions.Direction.Backward:
                    return 5;
            }
            return 0;
        }

        public void Enabled(bool enabled)
        {
            foreach (IMyGyro gyro in gyros)
            {
                if (gyro.Enabled != enabled) gyro.Enabled = enabled;
            }
        }

        public void SetGyroOverride(bool bOverride)
        {
            CheckGyro();

            for (int i = 0; i < gyros.Count; i++)
            {
                if (i == activeGyro) gyros[i].GyroOverride = bOverride;
                else gyros[i].GyroOverride = false;
            }
        }

        public void SetGyroYaw(float yawRate)
        {
            CheckGyro();

            if (activeGyro < gyros.Count)
            {
                profiles[gyroYaw[activeGyro]](gyros[activeGyro], yawRate);
            }
        }

        public void SetGyroPitch(float pitchRate)
        {
            if (activeGyro < gyros.Count)
            {
                profiles[gyroPitch[activeGyro]](gyros[activeGyro], pitchRate);
            }
        }

        public void SetGyroRoll(float rollRate)
        {
            if (activeGyro < gyros.Count)
            {
                profiles[gyroRoll[activeGyro]](gyros[activeGyro], rollRate);
            }
        }

        public void ZeroTurnGyro()
        {
            for (int i = 0; i < gyros.Count; i++)
            {
                profiles[gyroYaw[i]](gyros[i], 0f);
                profiles[gyroPitch[i]](gyros[i], 0f);
            }
        }

        public void ResetGyro()
        {
            foreach (IMyGyro gyro in gyros)
            {
                gyro.Yaw = gyro.Pitch = gyro.Roll = 0f;
            }
        }

        private void CheckGyro()
        {
            while (activeGyro < gyros.Count)
            {
                if (gyros[activeGyro].IsFunctional)
                {
                    break;
                }
                else
                {
                    IMyGyro gyro = gyros[activeGyro];

                    gyro.Enabled = gyro.GyroOverride = false;
                    gyro.Yaw = gyro.Pitch = gyro.Roll = 0f;

                    activeGyro++;
                }
            }
        }
    }

    public class PDController
    {
        double lastInput;

        public double gain_p;
        public double gain_d;

        double second;

        public PDController(double pGain, double dGain, float stepsPerSecond = 60f)
        {
            gain_p = pGain;
            gain_d = dGain;
            second = stepsPerSecond;
        }

        public double Filter(double input, int round_d_digits)
        {
            double roundedInput = Math.Round(input, round_d_digits);

            double derivative = (roundedInput - lastInput) * second;
            lastInput = roundedInput;

            return (gain_p * input) + (gain_d * derivative);
        }

        public void Reset()
        {
            lastInput = 0;
        }
    }

    // What's a torpedo tube?
    // To launch a torpedo, trigger the release block
    public class TorpedoTube
    {
        public List<IMyShipWelder> Welders = new List<IMyShipWelder>(); // [TRP1] Welder, etc
        public Dictionary<string, IMyProjector> Projectors = new Dictionary<string, IMyProjector>(); // [TRP1]<HE>, [TRP1]<CLST>, etc

        IMySensorBlock Bounder;
        public IMyTimerBlock Release; // [TRP1] Release
        public Torpedo LoadedTorpedo;

        MyGridProgram Program;
        TorpedoSubsystem Host;

        IMyProjector ActiveProjector = null;

        public TorpedoTube(MyGridProgram program, TorpedoSubsystem host)
        {
            Program = program;
            Host = host;
        }

        public void AddPart(IMyTerminalBlock block)
        {
            if (block is IMyShipWelder) Welders.Add((IMyShipWelder)block);
            else if (block is IMyProjector)
            {
                var projector = (IMyProjector)block;
                int start = block.CustomName.IndexOf('<');
                int end = block.CustomName.IndexOf('>');
                if (start == -1 || end == -1) return;
                string name = projector.CustomName.Substring(start, end - start);
                Projectors.Add(name, projector);
                if (projector.Enabled)
                {
                    if (ActiveProjector != null) ActiveProjector.Enabled = false;
                    ActiveProjector = projector;
                }
            }
            else if (block is IMyTimerBlock) Release = (IMyTimerBlock)block;
            else if (block is IMySensorBlock) Bounder = (IMySensorBlock)block;
        }

        private IMyTerminalBlock GetBlockFromReferenceAndPosition(IMyTerminalBlock reference, Vector3I position)
        {
            var matrix = new MatrixI(reference.Orientation);

            // -x left +x right
            // -y down +y up
            // -z forward +z backwards

            var pos = new Vector3I(-position.X, position.Y, -position.Z);
            Vector3I transformed;
            Vector3I.Transform(ref pos, ref matrix, out transformed);
            transformed += reference.Position;
            var slim = reference.CubeGrid.GetCubeBlock(transformed);
            return slim == null ? null : slim.FatBlock as IMyTerminalBlock;
        }

        public void GetTorpedo()
        {
            LoadedTorpedo = new Torpedo();

            var size = Bounder.CubeGrid.GridSize;

            int xmin = -(int)Math.Ceiling((Bounder.RightExtend - (size * 0.5)) / size);
            int xmax = (int)Math.Ceiling((Bounder.LeftExtend - (size * 0.5)) / size);
            int ymin = -(int)Math.Ceiling((Bounder.BottomExtend - (size * 0.5)) / size);
            int ymax = (int)Math.Ceiling((Bounder.TopExtend - (size * 0.5)) / size);
            int zmin = -(int)Math.Ceiling(Bounder.BackExtend / size);
            int zmax = (int)Math.Ceiling((Bounder.FrontExtend - size) / size);

            for (int x = xmin; x <= xmax; x++)
            {
                for (int y = ymin; y <= ymax; y++)
                {
                    for (int z = zmin; z <= zmax; z++)
                    {
                        var part = GetBlockFromReferenceAndPosition(Bounder, new Vector3I(x, y, z));
                        if (part != null) LoadedTorpedo.AddPart(part);
                    }
                }
            }
        }
    }
}
