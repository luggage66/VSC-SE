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

// by Luggage66

bool debugOutput = true;
float DRIVING_WHEEL_OFFSET = -0.50f; 
float CONNECTOR_WHEEL_OFFSET = 0.35f;
string STATUS_DISPLAY_NAME = "LCD 1";
 
public void Main(string argument, UpdateType updateSource)
{
    if (argument != null) {
        InitializePartReferences();
        RegisterArgumentWorkflow(argument, "ride-normal", SetModeNormal);
        RegisterArgumentWorkflow(argument, "ride-low", SetModeLow);
        RegisterArgumentWorkflow(argument, "extend-gear", ExtendGear);
        RegisterArgumentWorkflow(argument, "jack-up", JackUp);

        if (argument == "kill") {
            KillStateMachine();
        }
    }

    if ((updateSource & (UpdateType.Update100)) != 0)
    {
        RunStateMachine();
    }

    OutputConsoleBuffer();
}

public IEnumerable<bool> SetModeNormal() {
    this.Log_Debug("Set Mode: Normal");
    foreach (var x in RetractAndUnlock()) yield return x;
    foreach (var x in SetRideHeight(DRIVING_WHEEL_OFFSET)) yield return x;
}

public IEnumerable<bool> SetModeLow() {
    this.Log_Debug("Set Mode: Low");
    foreach (var x in RetractAndUnlock()) yield return x;
    foreach (var x in SetRideHeight(CONNECTOR_WHEEL_OFFSET)) yield return x;
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

    AppendToConsole("");
    while (direction * sampleWheel.GetValue<float>("Height") < (direction * desiredHeight - float.Epsilon * direction)) {
        ReplaceConsoleLine("Suspension: ");
        DoAll<IMyMotorSuspension>(susp => {
            var value = susp.GetValue<float>("Height");
            ReplaceConsoleLine(GetLastConsoleLine() + " " + value.ToString());
            if (direction * value < direction * desiredHeight - float.Epsilon * direction) {
                value += offset;
                susp.SetValue("Height", value);
            }
        });
        yield return true;
    }
}

public IEnumerable<bool> ExtendGear() {
    foreach (var x in ExtendToWheelHeightAndLock(GetPistonLengthForWheelPosition())) yield return x;
}

public IEnumerable<bool> JackUp() {
    var sampleLeg = GetFirstBlock<IMyPistonBase>(legPistons);
    var legPosition = GetPositionFromDetailedInfo(sampleLeg.DetailedInfo);
    var pistonLengthForWheelPosition = GetPistonLengthForWheelPosition();
    if (legPosition < pistonLengthForWheelPosition - float.Epsilon) {
        foreach (var x in ExtendToWheelHeightAndLock(GetPistonLengthForWheelPosition())) yield return x;
    }
    foreach (var x in ExtendToWheelHeightAndLock(2.0f)) yield return x;
}

public float GetPistonLengthForWheelPosition() {
    var sampleWheel = GetFirstBlock<IMyMotorSuspension>();
    var wheelOffest = sampleWheel.GetValue<float>("Height");
    return (-1 * wheelOffest + 0.5f);
}

public IEnumerable<bool> ExtendToWheelHeightAndLock(float limit = 0f, float speed = 0.1f) {
    this.Log_Debug("Extending landing gear");

    limit += 0.15f;

    limit = Math.Min(2.0f, limit);
    
    var sampleLeg = GetFirstBlock<IMyPistonBase>(legPistons);    
    var pistonLengthForWheelPosition = GetPistonLengthForWheelPosition();
    var LEG_STUCK_DETECT_SPACING = 0.1f;

    DoAll<IMyPistonBase>(legPistons, piston => {
        piston.MaxLimit = limit;
        piston.Velocity = speed;
    });

    var unlockPosition = 0f;
    var legPosition = 2.0f;
    var lastLegPosition = legPosition + LEG_STUCK_DETECT_SPACING * 2;

    // wait for legs to retract to wheel height before unlocking
    while (legPosition < limit - float.Epsilon) {
        unlockPosition = pistonLengthForWheelPosition;
        legPosition = GetPositionFromDetailedInfo(sampleLeg.DetailedInfo);

        if (Math.Abs(legPosition - lastLegPosition) < LEG_STUCK_DETECT_SPACING) {
            // unlock early, we may be stuck on ourselves.
            this.Log_Debug("Warn: Gear Stuck, unlocking.");
            DoAll<IMyLandingGear>(landingGear, gear => gear.Unlock());
        }

        lastLegPosition = legPosition;

        yield return true;
    }

    DoAll<IMyLandingGear>(landingGear, gear => gear.AutoLock = true);
    yield return true;
    DoAll<IMyLandingGear>(landingGear, gear => gear.Lock());
    yield return true;
    DoAll<IMyLandingGear>(landingGear, gear => gear.AutoLock = false);
    yield return true;
}

public IEnumerable<bool> RetractAndUnlock(float limit = 0f, float speed = 0.1f) {
    this.Log_Debug("Retracting Landing Gear.");

    var sampleLeg = GetFirstBlock<IMyPistonBase>(legPistons);
    var pistonLengthForWheelPosition = GetPistonLengthForWheelPosition();
    var LEG_STUCK_DETECT_SPACING = 0.1f;

    DoAll<IMyShipConnector>(connector => connector.Disconnect());
    yield return true;

    DoAll<IMyPistonBase>(legPistons, piston => {
        piston.MinLimit = limit;
        piston.Velocity = speed * -1;
    });

    var unlockPosition = 0f;
    var legPosition = 2.0f;
    var lastLegPosition = legPosition + LEG_STUCK_DETECT_SPACING * 2;

    // wait for legs to retract to wheel height before unlocking
    while (legPosition > unlockPosition + float.Epsilon) {
        unlockPosition = pistonLengthForWheelPosition;
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

private void RegisterArgumentWorkflow(string argument, string name, Func<IEnumerable<bool>> workflowFn) {
    if (argument == name) {
        CallWorkflow(workflowFn);
    }
}

private void CallWorkflow(Func<IEnumerable<bool>> workflowFn) {
    if (_stateMachine != null) {
        return; // only one workflow at a time.
    }

    _stateMachine = workflowFn().GetEnumerator();
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
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
    
    if (_stateMachine != null) {
        if (!_stateMachine.MoveNext() || !_stateMachine.Current) {
  _stateMachine.Dispose();
            _stateMachine = null;
            Runtime.UpdateFrequency = UpdateFrequency.None;
            this.Log_Debug("Workflow Complete.");
        }
    }
}

private void KillStateMachine() {
    _stateMachine = null;
    Runtime.UpdateFrequency = UpdateFrequency.None;
    this.Log_Debug("WORKFLOW KILLED!");
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
IMyTextSurface _display1;
MyIni config;
public Program()
{
    _textPanel = Me.GetSurface(0); //GetFirstBlock<IMyCockpit>().GetSurface(0); /
    _display1 = GridTerminalSystem.GetBlockWithName(STATUS_DISPLAY_NAME) as IMyTextSurface;
    var ini = new MyIni();

    MyIniParseResult result;
    if (!ini.TryParse(Me.CustomData, out result)) 
        throw new Exception(result.ToString());
    ini.Get("global", "WheelOffsetRidingPosition").ToString();
    // Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;
    UpdateStatusDisplay();
}

public void Log_Debug(string message) {
    if (!debugOutput) return;
    
    AppendToConsole(message);
    OutputConsoleBuffer();
}

private void UpdateStatusDisplay() {
    // _display1.Font = "monospace";
    _display1.FontColor = new Color(255,255,0);

    //_display1.SetValue<Single>( "FontSize", (Single)20 );  // set font size 
    _display1.WriteText("Mode: Normal");
    _display1.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
}

IList<string> consoleBuffer = new List<string>();
int CONSOLE_BUFFER_LENGTH = 15;
private void AppendToConsole(string message) {
    consoleBuffer.Add(message);
    var bufferLength = consoleBuffer.Count();
    if (bufferLength > CONSOLE_BUFFER_LENGTH) {
        consoleBuffer = consoleBuffer.Skip(Math.Max(0, bufferLength - CONSOLE_BUFFER_LENGTH)).ToList();
    }
}

private void ReplaceConsoleLine(string message) {
    consoleBuffer = consoleBuffer.Take(Math.Max(0, consoleBuffer.Count() - 1)).Concat(new string[] { message }).ToList();
}

private string GetLastConsoleLine() {
    var bufferLength = consoleBuffer.Count();
    return bufferLength > 0 ? consoleBuffer[bufferLength - 1] : "";
}

private void OutputConsoleBuffer() {
    var truncatedText = string.Join("\n", consoleBuffer);
    _textPanel.WriteText(truncatedText, false);
    _textPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
}


#region PreludeFooter
    }
}
#endregion