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

// Change this namespace for each script you create.
namespace SpaceEngineers.Luggage66.Minivan {
    public sealed class Program : MyGridProgram {
    // Your code goes between the next #endregion and #region
#endregion

IMyTextSurface _textPanel;
string _header = "Booting";
public void Log_Debug(string message) {
    
    var text = _textPanel.GetText();
    text += "\n" + message;
    var lines = text.Split('\n');
    var truncatedText = string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 15)));

    _textPanel.WriteText(_header + "\n" + truncatedText, false);
    _textPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
}

public Program()
{
    // Runtime.UpdateFrequency = UpdateFrequency.Once | UpdateFrequency.Update100;
    _textPanel = Me.GetSurface(0);
    this.Log_Debug("Program()");
}

public void Save()
{
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument != null) {
        if (argument == "reset-gear") {
            _stateMachine = this.ResetGear().GetEnumerator();
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }
    }

    if ((updateSource & (UpdateType.Update100)) != 0)
    {
        RunStateMachine();
    }
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

string legPistonGroupName = "Leg Pistons";
IMyBlockGroup _landingGearPistons;
// IMyMotorSuspension _susp;
public IEnumerable<bool> ResetGear() {
    this.Log_Debug("ResetGear() Begin");
    yield return true;

    // 1. Begin Retracting
    this.Log_Debug("ResetGear() Retracting");
    
    yield return true;

    // 2. Release landing gear locks
    this.Log_Debug("ResetGear() Unlock");

    yield return true;

    // 3. Extend Wheels
    this.Log_Debug("ResetGear() Extend");
    yield return true;

    this.Log_Debug("ResetGear() Complete");
}

#region PreludeFooter
    }
}
#endregion