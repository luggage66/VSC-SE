#region Prelude
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame.Utilities;

// Change this namespace for each script you create.
namespace SpaceEngineers.Luggage66.Minivan {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

/*
[global]
WheelOffsetRidingPosition=50
WheelOffsetConnectorPosition=50
LegsExtendedPositionAtRideHeight=1
LegsExtendedPositionAtConnectorHeight=0.1

[command:reset-gear]
AnotherKey=Another value
*/

bool debugOutput = true;
float DRIVING_WHEEL_OFFSET = -0.50f; 
float CONNECTOR_WHEEL_OFFSET = 0.35f;

float LEG_HEIGHT_UNLOCK = 1.0f;
 
public void Log_Debug(string message) {
    if (!debugOutput) return;
    
    var text = _textPanel.GetText();
    text += "\n" + message;
    var lines = text.Split('\n');
    var truncatedText = string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 15)));

    _textPanel.WriteText(_header + "\n" + truncatedText, false);
    _textPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
}

MyIni config;
public Program()
{
    _textPanel = Me.GetSurface(0); //GetFirstBlock<IMyCockpit>().GetSurface(0); /
    var ini = new MyIni();

    MyIniParseResult result;
    if (!ini.TryParse(Me.CustomData, out result)) 
        throw new Exception(result.ToString());
    ini.Get("global", "WheelOffsetRidingPosition").ToString();
    // Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;
    this.Log_Debug("Program()");
}

public void Save()
{
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument != null) {
        if (argument == "ride-normal") {
            _stateMachine = this.SetModeNormal().GetEnumerator();
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        if (argument == "ride-low") {
            _stateMachine = this.FullLower().GetEnumerator();
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }   
    }

    if ((updateSource & (UpdateType.Update100)) != 0)
    {
        RunStateMachine();
    }
}

string legPistonGroupName = "Leg Pistons";
string landingGearGroupName = "Piston Landing Gear";
IMyBlockGroup legPistons;
IMyBlockGroup landingGear;
private void InitializePartReferences() {
    legPistons = GridTerminalSystem.GetBlockGroupWithName(legPistonGroupName);
    landingGear = GridTerminalSystem.GetBlockGroupWithName(landingGearGroupName);
}

IEnumerator<bool> _stateMachine;
public void RunStateMachine() {
    
    _header = "Running...";
    if (_stateMachine != null) {
        if (!_stateMachine.MoveNext() || !_stateMachine.Current) {
  _stateMachine.Dispose();
            _stateMachine = null;
            this.Log_Debug("Disposed()");
            Runtime.UpdateFrequency = UpdateFrequency.None;
            _header = "Stopped.";
        }
    }
}

public IEnumerable<bool> SetModeNormal() {
    InitializePartReferences();

    this.Log_Debug("Disconnecting and Raising Legs.");   

    foreach (bool x in RetractAndUnlock()) {
        yield return x;
    }

    // 2. Release landing gear locks
    this.Log_Debug("Unlocking Landing Gear.");
    DoAll<IMyLandingGear>(landingGear, gear => gear.Unlock());

    // 3. Extend Wheels
    foreach (bool x in SetRideHeight(DRIVING_WHEEL_OFFSET)) {
        yield return x;
    }
}

public IEnumerable<bool> SetRideHeight(float desiredHeight = -0.5f) {
    this.Log_Debug("Setting Ride Height to " + desiredHeight.ToString());
    var sampleWheel = GetFirstBlock<IMyMotorSuspension>();
    var max = sampleWheel.GetMaximum<float>("Height");
    var min = sampleWheel.GetMinimum<float>("Height");
    var current = sampleWheel.GetValue<float>("Height");

    desiredHeight = Math.Min(max, desiredHeight);
    desiredHeight = Math.Max(min, desiredHeight);

    var direction = Math.Sign(desiredHeight - current);

    var offset = direction < 0 ? -0.1f : 0.1f;

    this.Log_Debug("Max " + max.ToString());
    this.Log_Debug("Min " + min.ToString());

    while (direction * sampleWheel.GetValue<float>("Height") < (direction * desiredHeight - float.Epsilon * direction)) {
        DoAll<IMyMotorSuspension>(susp => {
            var value = susp.GetValue<float>("Height");
            if (direction * value < direction * desiredHeight - float.Epsilon * direction) {
                value += offset;
                susp.SetValue("Height", value);
            }
        });
        yield return true;
    }
}

public IEnumerable<bool> RetractAndUnlock(float limit = 0f, float speed = 0.1f) {
    var sampleWheel = GetFirstBlock<IMyMotorSuspension>();
    var sampleLeg = GetFirstBlock<IMyPistonBase>(legPistons);
    var wheelOffest = sampleWheel.GetValue<float>("Height");
    var LEG_STUCK_DETECT_SPACING = 0.1f;

    DoAll<IMyShipConnector>(connector => connector.Disconnect());
    yield return true;

    DoAll<IMyPistonBase>(legPistons, piston => {
        piston.MinLimit = limit;
        piston.Velocity = speed * -1;
    });

    var startingPosition = GetPositionFromDetailedInfo(sampleLeg.DetailedInfo);
    var unlockPosition = 0f;
    var legPosition = 2.0f;
    var lastLegPosition = legPosition + LEG_STUCK_DETECT_SPACING * 2;

    // wait for legs to retract to wheel height before unlocking
    while (legPosition > unlockPosition) {
        unlockPosition = (-1 * wheelOffest + 0.5f);
        legPosition = GetPositionFromDetailedInfo(sampleLeg.DetailedInfo);

        if (Math.Abs(legPosition - lastLegPosition) < LEG_STUCK_DETECT_SPACING) {
            // unlock early, we may be stuck on ourselves.
            this.Log_Debug("Warn: Gear Stuck, unlocking.");
            DoAll<IMyLandingGear>(landingGear, gear => gear.Unlock());
        }

        lastLegPosition = legPosition;

        yield return true;
    }

    DoAll<IMyLandingGear>(landingGear, gear => gear.Unlock());
}

public IEnumerable<bool> FullLower() {
    InitializePartReferences();

    this.Log_Debug("Retracting Legs.");
    foreach (bool x in RetractAndUnlock()) {
        yield return x;
    }

    foreach (bool x in SetRideHeight(CONNECTOR_WHEEL_OFFSET)) {
        yield return x;
    }
}

private TBlockType GetFirstBlock<TBlockType>(IMyBlockGroup group) where TBlockType : class, IMyTerminalBlock {
    var list = new List<IMyTerminalBlock>(1);
    group.GetBlocks(list);
    return list[0] as TBlockType;
}

private TBlockType GetFirstBlock<TBlockType>() where TBlockType : class, IMyTerminalBlock {
    var list = new List<IMyTerminalBlock>(1);
    GridTerminalSystem.GetBlocksOfType<TBlockType>(list, block => block.CubeGrid == Me.CubeGrid); 
    return list[0] as TBlockType;
}

private float GetPositionFromDetailedInfo(string info) {
    var lines = info.Split('\n');
    var positionLine = lines
        .Select(line => line.Split(':'))
        .Where(parts => parts[0] == "Current position")
        .First();

    return float.Parse(positionLine[1].Replace("m", ""));
}

public void DoAll<TTerminalBlock>(IMyBlockGroup group, Action<TTerminalBlock> action) where TTerminalBlock : class, IMyTerminalBlock {
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(); 
    group.GetBlocksOfType<TTerminalBlock>(blocks); 

    for (int index = 0; index < blocks.Count; index++) 
    { 
        TTerminalBlock block = blocks[index] as TTerminalBlock;
        action(block);
    }
}
public void DoAll<TTerminalBlock>(Action<TTerminalBlock> action) where TTerminalBlock : class, IMyTerminalBlock {
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.GetBlocksOfType<TTerminalBlock>(blocks, block => block.CubeGrid == Me.CubeGrid); 

    for (int index = 0; index < blocks.Count; index++) 
    { 
        TTerminalBlock block = blocks[index] as TTerminalBlock;
        action(block);
    }
}

IMyTextSurface _textPanel;
string _header = "Booting";

#region PreludeFooter
    }
}
#endregion