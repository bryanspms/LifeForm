using System.Collections.Generic;
using UnityEngine;

public struct Experience
{
    public float[] State;
    public int Action;
    public float Reward;
    public float[] NextState;
    public bool Done;
    public bool IsObservation;
}

public class ReplayBuffer
{
    private List<Experience> buffer;
    private int capacity;
    private int index = 0;

    public ReplayBuffer(int capacity = 5000)
    {
        this.capacity = capacity;
        buffer = new List<Experience>(capacity);
    }

    public void Push(float[] state, int action, float reward, float[] nextState, bool done, bool isObs)
    {
        Experience exp = new Experience {
            State = state, Action = action, Reward = reward, 
            NextState = nextState, Done = done, IsObservation = isObs
        };

        if (buffer.Count < capacity) buffer.Add(exp);
        else buffer[index] = exp;
        index = (index + 1) % capacity;
    }

    public Experience Sample()
    {
        return buffer[Random.Range(0, buffer.Count)];
    }

    public int Count => buffer.Count;
}