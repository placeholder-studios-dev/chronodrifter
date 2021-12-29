using System.Collections.Generic;
using UnityEngine;

// General class for a time reversible object. Maintains a stack of previous
// states. Forward time adds new states to the stack, and reverse pops them.

public abstract class TimeReversibleObject : MonoBehaviour {
    // stack of previous states.
    private State[] objectHistory;
    private State state;
    private State prevState;

    // for use with slowed time
    private State nextState;
    private int timeStepProgress;
    private int timeStepsPerState = 1;
    private int tmpStepProgress;
    private int tmpSteps;
    
    private int lastStateIdx = -1;
    private int maxStackSize;
    public float similarityThreshold = 5e-2f;
    private float maxHistorySeconds = 5*60; // five minutes

    void Start() {
        maxStackSize = (int) Mathf.Round(maxHistorySeconds/Time.fixedDeltaTime);
        objectHistory = new State[maxStackSize];
        ChildStart();

        TimeEventManager.OnPause += UpdateOnPause;
        TimeEventManager.OnReverse += UpdateOnReverse;

        prevState = GetCurrentState();
        nextState = GetCurrentState();
        state = prevState;
    }

    void FixedUpdate() {
        // case where paused with infinite slowdown is handled separately in inheriting classes.
        if (!(TimeEventManager.isPaused && float.IsPositiveInfinity(TimeEventManager.slowFactor))) {
            if (TimeEventManager.isReversed) {
                if (tmpStepProgress >= 0) {
                    if (tmpStepProgress == 0) {
                        state = prevState;
                        timeStepProgress = timeStepsPerState-1;
                        if (lastStateIdx > 0) {
                            prevState = objectHistory[lastStateIdx];
                            if (prevState.numIntervals <= 1) {
                                lastStateIdx--;
                            }
                            prevState.numIntervals--;
                        }
                        else if (lastStateIdx == 0) {
                            prevState = objectHistory[lastStateIdx];
                            OnStackSize1(prevState);
                            prevState.numIntervals--;
                        }
                        else {
                            OnStackEmpty();
                        }
                    }
                    else {
                        if (GetStateDifference(prevState, nextState) < 20) {
                            state = StateLerp(prevState, nextState, (float) (tmpStepProgress) / tmpSteps);
                        }
                        else {
                            if (2*tmpStepProgress < tmpSteps) {
                                state = prevState;
                            }
                            else {
                                state = nextState;
                            }
                        }
                    }
                    tmpStepProgress--;
                }
                
                else {
                    if (timeStepProgress == timeStepsPerState-1) {
                        nextState = prevState;

                        if (lastStateIdx > 0) {
                            prevState = objectHistory[lastStateIdx];
                            if (prevState.numIntervals <= 1) {
                                lastStateIdx--;
                            }
                            prevState.numIntervals--;
                        }
                        else if (lastStateIdx == 0) {
                            prevState = objectHistory[lastStateIdx];
                            OnStackSize1(prevState);
                            prevState.numIntervals--;
                        }
                        else {
                            OnStackEmpty();
                        }
                    }
                    
                    if (GetStateDifference(prevState, nextState) < 20) {
                        state = StateLerp(prevState, nextState, (float) (timeStepProgress) / timeStepsPerState);
                    }
                    else {
                        if (2*timeStepProgress < timeStepsPerState) {
                            state = prevState;
                        }
                        else {
                            state = nextState;
                        }
                    }

                    timeStepProgress = (timeStepProgress - 1 + timeStepsPerState) % timeStepsPerState;
                }
                if (TimeEventManager.isPaused) {
                    state = SlowDownState(state);
                }
                UpdateObjectState(state);
            }
            else {
                if (timeStepProgress == timeStepsPerState-1) {
                    state = GetCurrentState();

                    if (TimeEventManager.isPaused) {
                        state = SpeedUpState(state);
                    }
                    if (lastStateIdx >= 0) {
                        State s = objectHistory[lastStateIdx];
                        if (GetStateDifference(state, s) < similarityThreshold) {
                            s.numIntervals++;
                            state = s;
                        }
                        else if (lastStateIdx < maxStackSize - 1) {
                            lastStateIdx++;
                            objectHistory[lastStateIdx] = state;
                        }
                    }
                    else if (lastStateIdx < maxStackSize - 1) {
                        lastStateIdx++;
                        objectHistory[lastStateIdx] = state;
                    }
                    nextState = prevState = state;
                }
                timeStepProgress = (timeStepProgress + 1) % timeStepsPerState;
            }
        }
        ChildFixedUpdate();
    }

    void UpdateOnPause() {
        tmpStepProgress = -1;

        timeStepsPerState = (int) TimeEventManager.curSlowFactor;
        if (TimeEventManager.isPaused) {
            timeStepProgress = timeStepsPerState - 1;
        }
        else {
            timeStepProgress = 0;
        }
    }

    void UpdateOnReverse() {
        if (TimeEventManager.isReversed && TimeEventManager.isPaused) {
            nextState = GetCurrentState();
            tmpSteps = timeStepProgress;
            tmpStepProgress = tmpSteps - 1;
        }
        else {
            tmpStepProgress = -1;
        }
    }

    // The following functions can be implemented by each child class.
    public virtual State GetCurrentState() {return new State();}
    public virtual float GetStateDifference(State state1, State state2) {return 0.0f;}
    public virtual void UpdateObjectState(State s) {}
    public virtual void OnStackSize1(State s) {}
    public virtual void OnStackEmpty() {}
    public virtual void ChildStart() {}
    public virtual void ChildFixedUpdate() {}
    public virtual State StateLerp(State state1, State state2, float f) {return state2;}
    public virtual State SlowDownState(State state) {return state;}
    public virtual State SpeedUpState(State state) {return state;}
}

// child classes of TimeReversibleObject will implement a specific State class.
public class State : Object {
    // Number of FixedUpdate iterations for which the object has been in this State.
    public int numIntervals = 1;
}