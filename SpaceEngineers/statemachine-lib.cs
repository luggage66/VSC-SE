class StateMachineLib {
IMyTimerBlock _timer;
IMyInteriorLight _panelLight;
IMyTextPanel _textPanel;
IEnumerator<bool> _stateMachine;

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    // Retrieve the blocks we're going to use.
    _timer = GridTerminalSystem.GetBlockWithName("Timer Block") as IMyTimerBlock;
    _panelLight = GridTerminalSystem.GetBlockWithName("Interior Light") as IMyInteriorLight;
    _textPanel = GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextPanel;

    // Initialize our state machine
    _stateMachine = RunStuffOverTime().GetEnumerator();

    // Start the timer to run the first instruction set. Depending on your script, you may want to use
    // TriggerNow rather than Start. Just be very careful with that, you can easily bog down your
    // game that way.
    _timer.ApplyAction("Start");
}


public void Main(string argument) {

    // Usually I verify that the argument is empty or a predefined value before running the state
    // machine. This way we can use arguments to control the script without disturbing the
    // state machine and its timing. For the purpose of this example however, I will omit this.

    // ***MARKER: State Machine Execution
    // If there is an active state machine, run its next instruction set.
    if (_stateMachine != null) {
        // If there are no more instructions, or the current value of the enumerator is false,
        // we stop and release the state machine.
        if (!_stateMachine.MoveNext() || !_stateMachine.Current) {
  _stateMachine.Dispose();
            _stateMachine = null;
        } else {
            // The state machine has more work to do. Restart the timer. Again you might choose
            // to use TriggerNow.
            _timer.ApplyAction("Start");
        }
    }
}


// ***MARKER: State Machine Program
// NOTE: If you don't need a precreated, reusable state machine,
// replace the declaration with the following:
// public IEnumerator<bool> RunStuffOverTime() {
public IEnumerable<bool> RunStuffOverTime() {
    // For the very first instruction set, we will just switch on the light.
    _panelLight.RequestEnable(true);

    // Then we will tell the script to stop execution here and let the game do it's
    // thing. The time until the code continues on the next line after this yield return
    // depends  on your State Machine Execution and the timer setup.
    // I have chosen the simplest form of state machine here. A return value of
    // true will tell the script to execute the next step the next time the timer is
    // invoked. A return value of false will tell the script to halt execution and not
    // continue. This is actually not really necessary, you could have used the
    // statement
    //      yield break;
    // to do the same. However I wanted to demonstrate how you can use the return
    // value of the enumerable to control execution.
    yield return true;

    int i = 0;
    // The following would seemingly be an illegal operation, because the script would
    // keep running until the instruction count overflows. However, using yield return,
    // you can get around this limitation.
    while (true) {
        _textPanel.WritePublicText(i.ToString());
        i++;
        // Like before, when this statement is executed, control is returned to the game.
        // This way you can have a continuously polling script with complete state
        // management, with very little effort.
        yield return true;
    }
}

// ***MARKER: Alternative state machine runner: This variant is a one-shot method.
// The previous version can be precreated and then run multiple times. This version
// creates an iterator directly. Select the one matching your use.
public IEnumerator<bool> RunStuffOverTime() {
    // For the very first instruction set, we will just switch on the light.
    _panelLight.RequestEnable(true);

    // Then we will tell the script to stop execution here and let the game do it's
    // thing. The time until the code continues on the next line after this yield return
    // depends  on your State Machine Execution and the timer setup.
    // I have chosen the simplest form of state machine here. A return value of
    // true will tell the script to execute the next step the next time the timer is
    // invoked. A return value of false will tell the script to halt execution and not
    // continue. This is actually not really necessary, you could have used the
    // statement
    //      yield break;
    // to do the same. However I wanted to demonstrate how you can use the return
    // value of the enumerable to control execution.
    yield return true;

    int i = 0;
    // The following would seemingly be an illegal operation, because the script would
    // keep running until the instruction count overflows. However, using yield return,
    // you can get around this limitation.
    while (true) {
        _textPanel.WritePublicText(i.ToString());
        i++;
        // Like before, when this statement is executed, control is returned to the game.
        // This way you can have a continuously polling script with complete state
        // management, with very little effort.
        yield return true;
    }
}

}