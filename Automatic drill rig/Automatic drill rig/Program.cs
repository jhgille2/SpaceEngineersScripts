using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;



namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        // A list to hold all attached cameras, pistons, and rotor
        List<IMyPistonBase> UpPistons         = new List<IMyPistonBase>();
        List<IMyPistonBase> DownPistons       = new List<IMyPistonBase>();
        List<IMyPistonBase> HorizontalPistons = new List<IMyPistonBase>();
        List<IMyMotorRotor> AllRotors         = new List<IMyMotorRotor>();
        List<IMyShipDrill> AllDrills          = new List<IMyShipDrill>();

        // Lists to hold the current piston extention limits
        List<float> DownPistonLimits = new List<float>();
        List<float> UpPistonLimits   = new List<float>();


        float raycast_distance = 50;
        float swingarmspeed = 0.1F;
        float tickCounter = 0;

        bool firstRun = true;
        bool initComplete = false;
        bool drillstart = false;
        bool firstswing = true;

        float DownDiffTotal = 0;
        float UpDiffTotal = 0;



        // Stringbuilder to hold text to add text to output
        private StringBuilder sb = new StringBuilder();

        // Variables to hold a camera and a display lcd
        private IMyCameraBlock rangeCamera;
        private IMyTextPanel displayLCD;
        private MyDetectedEntityInfo rangeInfo;

        // Structures for drill head and base rotors
        public IMyMotorStator drillHeadRotor;
        public IMyMotorStator basePivotRotor;

        bool atmaxrot = false;
        bool atminrot = false;

        public static float PistonDifferential(float CurrentPistonHeight, float MaxPistonHeight)
        {
            return MaxPistonHeight - CurrentPistonHeight;
        }

        // Initialize command line parser
        //MyCommandLine _commandLine = new MyCommandLine();

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            // Fill the lists for the cameras, panels, pistons, and rotors
            // GridTerminalSystem.GetBlocksOfType(AllPistons);
            // GridTerminalSystem.GetBlocksOfType(AllRotors);

            // float minRotation = float.Parse(_commandLine.Argument(0));
            // float maxRotation = float.Parse(_commandLine.Argument(1));

            // If it is the first time the script is being run, detect a single named camera and lcd and initialize variables with their respective structures
            // Also, move pistons to their starting positions.

            tickCounter = tickCounter + 1;
            if (tickCounter == 20)
            {
                tickCounter = 0;
            }

            
            if (firstRun)
            {
                firstRun = false;
                rangeCamera = (IMyCameraBlock)GridTerminalSystem.GetBlockWithName("range camera");
                rangeCamera.EnableRaycast = true;

                displayLCD = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("summary lcd");

                // Get the named piston block groups. For now, these have to be named UPPISTONS, DOWNPISTONS, and HORIZONTALPISTONS
                IMyBlockGroup upPiston_group = GridTerminalSystem.GetBlockGroupWithName("UPPISTONS");
                IMyBlockGroup downPiston_group = GridTerminalSystem.GetBlockGroupWithName("DOWNPISTONS");
                IMyBlockGroup horizontalPiston_group = GridTerminalSystem.GetBlockGroupWithName("HORIZONTALPISTONS");

                // Get lists of the pistons in each block group
                upPiston_group.GetBlocksOfType(UpPistons);
                downPiston_group.GetBlocksOfType(DownPistons);
                horizontalPiston_group.GetBlocksOfType(HorizontalPistons);

                // Get all drills
                GridTerminalSystem.GetBlocksOfType(AllDrills);

                // Initialize piston extensions
                foreach (IMyPistonBase piston in UpPistons)
                {
                    piston.MinLimit = 10;
                    piston.Velocity = 1;
                }

                foreach (IMyPistonBase piston in DownPistons)
                {
                    piston.MaxLimit = 0;
                    piston.Velocity = -1;
                }

                foreach (IMyPistonBase piston in HorizontalPistons)
                {
                    piston.MaxLimit = 10;
                    piston.Velocity = 1;
                }

                // Get the rotors by name
                drillHeadRotor = (IMyMotorStator)GridTerminalSystem.GetBlockWithName("DRILLHEADROTOR");
                basePivotRotor = (IMyMotorStator)GridTerminalSystem.GetBlockWithName("PIVOTROTOR");

                // Set pivot angle limits for the base rotor
                basePivotRotor.UpperLimitDeg = 115;
                basePivotRotor.LowerLimitDeg = -88;
                basePivotRotor.TargetVelocityRPM = (float)1;
            }

            DownPistonLimits = DownPistons.Select(P => P.MaxLimit).ToList();
            UpPistonLimits = UpPistons.Select(P => P.MaxLimit).ToList();

            if(Math.Round(basePivotRotor.Angle) == Math.Round((Math.PI / 180) * basePivotRotor.UpperLimitDeg))
            {
                initComplete = true;
            }


            if (initComplete)
            {

                if (!drillstart)
                {
                    foreach(IMyShipDrill drill in AllDrills)
                    {
                        drill.Enabled = true;
                    }

                    drillHeadRotor.TargetVelocityRPM = (float)2.5;

                    drillstart = true;
                }

                if (firstswing)
                {
                    firstswing = false;
                    swingarmspeed = swingarmspeed * -1F;

                    basePivotRotor.TargetVelocityRPM = swingarmspeed;
                    atmaxrot = true;
                }

                // Simple check to rotate the main pivot arm at the rotation limits
                if (Math.Round(basePivotRotor.Angle) == Math.Round((Math.PI / 180) * basePivotRotor.LowerLimitDeg) & !atminrot)
                {
                    atminrot = true;
                    atmaxrot = false;

                    // At the end of the main pivot, perform a raycast with the camera to see how far away the gound is
                    // and then subtract the length of the drill to get how far away from the ground the drill head is
                    rangeInfo = rangeCamera.Raycast(raycast_distance, 0, 0);
                    float travelDistance = (float)Vector3D.Distance(rangeCamera.GetPosition(), rangeInfo.HitPosition.Value) - 4;

                    // If the drillhead is more than 1 meter above the ground....
                    if (travelDistance > 1)
                    {
                        // First check if all the upward pistons have been retracted. If they haven't....
                        if (UpPistonLimits.All(limit => limit > 0))
                        {

                            // Calculate how far each piston needs to retract to cover the travel distance
                            float retractAmt = (float)Math.Ceiling(travelDistance / UpPistons.Count);

                            // Set the new max limit equal to the old max limit minus this differe
                            foreach(IMyPistonBase piston in UpPistons)
                            {
                                float newMaxlimit = Math.Max(piston.MaxLimit - retractAmt, 0);

                                piston.MaxLimit = newMaxlimit;
                                piston.MinLimit = piston.MaxLimit;
                                piston.Velocity = -0.1F;
                            }
                        }
                        else // Otherwise, extend the downward pistons in the same fashion
                        {

                            // Calculate how far each piston needs to retract to cover the travel distance and then 
                            // apply the new max limits for each piston according to this calculation
                            float extendAmt = (float)Math.Ceiling(travelDistance / DownPistons.Count);

                            foreach(IMyPistonBase piston in DownPistons)
                            {

                                float newMaxLimit = Math.Min(piston.MaxLimit + extendAmt, 10);

                                piston.MaxLimit = newMaxLimit;
                                piston.MinLimit = piston.MaxLimit;
                                piston.Velocity = 0.1F;
                            }

          
                        }
                    }

                    // Reverse the direction of the swinging arm
                    swingarmspeed = swingarmspeed * -1F;

                    basePivotRotor.TargetVelocityRPM = swingarmspeed;
                }

                if (Math.Round(basePivotRotor.Angle) == Math.Round((Math.PI / 180) * basePivotRotor.UpperLimitDeg) & !atmaxrot)
                {
                    atminrot = false;
                    atmaxrot = true;

                    swingarmspeed = swingarmspeed * -1F;

                    basePivotRotor.TargetVelocityRPM = swingarmspeed;
                }

                // Ending conditions (the downward pistons can no longer extend)
                if (DownPistonLimits.All(limit => limit == 10) & atmaxrot)
                {
                    basePivotRotor.TargetVelocityRPM = 0F;
                    drillHeadRotor.TargetVelocityRPM = 0F;

                    foreach (IMyShipDrill drill in AllDrills)
                    {
                        drill.Enabled = false;
                    }
                    foreach(IMyPistonBase piston in DownPistons)
                    {
                        piston.MaxLimit = 0;
                        piston.MinLimit = piston.MaxLimit;
                        piston.Velocity = -0.1F;
                    }
                    foreach(IMyPistonBase piston in UpPistons)
                    {
                        piston.MaxLimit = 10;
                        piston.MinLimit = 10;
                        piston.Velocity = 0.1F;
                    }

                    sb.Clear();
                    sb.Append("Drilling complete");

                    displayLCD.WritePublicText(sb.ToString());
                    displayLCD.ShowPrivateTextOnScreen();
                    displayLCD.ShowPublicTextOnScreen();

                    List<IMyProgrammableBlock> miningBlock = new List<IMyProgrammableBlock>();
                    GridTerminalSystem.GetBlocksOfType(miningBlock);
                    miningBlock[1].Enabled = false;

                }

                    // Cast a raycast directly in front of the camera and get the distance to the hit entity
                    if (rangeCamera.CanScan(raycast_distance))
                {
                    rangeInfo = rangeCamera.Raycast(raycast_distance, 0, 0);



                    if (rangeInfo.HitPosition.HasValue)
                    {

                        float travelDistance = (float)Vector3D.Distance(rangeCamera.GetPosition(), rangeInfo.HitPosition.Value) - 4;
                        



                        sb.Clear();
                        sb.Append("Hit: " + rangeInfo.HitPosition.Value.ToString("0.000"));
                        sb.AppendLine();
                        sb.Append("Distance: " + Vector3D.Distance(rangeCamera.GetPosition(), rangeInfo.HitPosition.Value).ToString("0.00"));
                        sb.AppendLine();
                        sb.Append("Distance Difference: " + travelDistance.ToString("0.00"));
                        sb.AppendLine();
                        sb.Append("Current rotor angle: " + basePivotRotor.Angle);
                        sb.AppendLine();
                        sb.Append("Expected starting position: " + Math.Round((Math.PI / 180) * basePivotRotor.UpperLimitDeg));
                        sb.AppendLine();
                        sb.Append("Init complete: " + initComplete);
                        sb.AppendLine();
                        for (int i = 0; i < DownPistonLimits.Count; ++i)
                        {
                            sb.Append("Current Downwards piston limits: " + DownPistonLimits[i]);
                            sb.AppendLine();
                        }
                        for (int i = 0; i < UpPistonLimits.Count; ++i)
                        {
                            sb.Append("Current Upwards piston limits: " + UpPistonLimits[i]);
                            sb.AppendLine();
                        }
                        sb.Append("Upwards piston differential: " + UpDiffTotal);
                        sb.AppendLine();
                        sb.Append("Downwards piston differential: " + DownDiffTotal);
                        sb.AppendLine();
                        sb.Append("At min rot limit: " + atminrot);
                        sb.AppendLine();
                        sb.Append("At max rot limit: " + atmaxrot);
                        sb.AppendLine();
                        sb.Append("Swing arm speed: " + swingarmspeed);
                        sb.AppendLine();

                    }

                    // Print some information to the lcd
                    displayLCD.WritePublicText(sb.ToString());
                    displayLCD.ShowPrivateTextOnScreen();
                    displayLCD.ShowPublicTextOnScreen();

                }



            }

            




            
        }
    }
}
