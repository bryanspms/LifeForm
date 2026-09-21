using System;

[System.Serializable]
public class NeuralNetwork
{
    public int inputDim;
    public int hiddenDim;
    public int outputDim;

    public float[][] w1; // input -> hidden
    public float[] b1;
    public float[][] w2; // hidden -> output
    public float[] b2;

    private static readonly Random rand = new Random();

    public NeuralNetwork(int inputDim, int hiddenDim, int outputDim)
    {
        this.inputDim = inputDim;
        this.hiddenDim = hiddenDim;
        this.outputDim = outputDim;

        w1 = AllocateMatrix(inputDim, hiddenDim);
        b1 = new float[hiddenDim];
        w2 = AllocateMatrix(hiddenDim, outputDim);
        b2 = new float[outputDim];

        InitializeWeights(w1, inputDim);
        InitializeWeights(w2, hiddenDim);
    }

    private float[][] AllocateMatrix(int rows, int cols)
    {
        float[][] m = new float[rows][];
        for (int i = 0; i < rows; i++) m[i] = new float[cols];
        return m;
    }

    private void InitializeWeights(float[][] matrix, int fanIn)
    {
        float limit = (float)Math.Sqrt(6.0 / fanIn);
        for (int i = 0; i < matrix.Length; i++)
            for (int j = 0; j < matrix[i].Length; j++)
                matrix[i][j] = (float)(rand.NextDouble() * 2 * limit - limit);
    }

    public float[] Forward(float[] input, out float[] hiddenOut)
    {
        hiddenOut = new float[hiddenDim];
        for (int j = 0; j < hiddenDim; j++)
        {
            float sum = b1[j];
            for (int i = 0; i < inputDim; i++) sum += input[i] * w1[i][j];
            hiddenOut[j] = sum > 0f ? sum : 0f; // ReLU
        }

        float[] output = new float[outputDim];
        for (int k = 0; k < outputDim; k++)
        {
            float sum = b2[k];
            for (int j = 0; j < hiddenDim; j++) sum += hiddenOut[j] * w2[j][k];
            output[k] = sum; // Linear output for Q-values
        }
        return output;
    }

    public void Train(float[] state, int action, float targetQ, float lr)
    {
        float[] hidden;
        float[] qValues = Forward(state, out hidden);
        float err = targetQ - qValues[action];

        // Gradient for output layer
        float[] dHidden = new float[hiddenDim];
        for (int j = 0; j < hiddenDim; j++)
        {
            float grad = err * hidden[j];
            w2[j][action] += lr * grad;
            dHidden[j] += err * w2[j][action];
        }
        b2[action] += lr * err;

        // Gradient for hidden layer (ReLU backprop)
        for (int j = 0; j < hiddenDim; j++)
        {
            if (hidden[j] <= 0f) continue;
            for (int i = 0; i < inputDim; i++)
            {
                w1[i][j] += lr * dHidden[j] * state[i];
            }
            b1[j] += lr * dHidden[j];
        }
    }

    public NeuralNetwork Clone()
    {
        NeuralNetwork copy = new NeuralNetwork(inputDim, hiddenDim, outputDim);
        Array.Copy(b1, copy.b1, b1.Length);
        Array.Copy(b2, copy.b2, b2.Length);
        for (int i = 0; i < inputDim; i++) Array.Copy(w1[i], copy.w1[i], hiddenDim);
        for (int j = 0; j < hiddenDim; j++) Array.Copy(w2[j], copy.w2[j], outputDim);
        return copy;
    }
}