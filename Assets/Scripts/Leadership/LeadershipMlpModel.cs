using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// =========================================================
// LEADERSHIP MLP MODEL
// =========================================================
// Runs the MLP exported by MLP_Training/train_leadership_mlp.py
// (StreamingAssets/Leadership/leadership_mlp.json).
// Forward pass: log1p -> standardise -> dense layers (ReLU) -> softmax.
// =========================================================

[Serializable]
public class MlpLayerData
{
    public int inputs;
    public int outputs;
    public string activation;
    public float[] weights;   // row-major: weights[i * outputs + j]
    public float[] biases;
}

[Serializable]
public class MlpModelData
{
    public int version;
    public string[] featureNames;
    public string inputTransform;
    public float[] scalerMean;
    public float[] scalerScale;
    public List<MlpLayerData> layers = new();
    public string[] classes;

    public string createdUtc;
    public string labelSource;
    public int syntheticSamples;
    public int realLabelledSamples;
    public float testAccuracy;
}

public class LeadershipMlpModel
{
    public MlpModelData Data { get; private set; }

    public int FeatureCount => Data.scalerMean.Length;

    public static LeadershipMlpModel TryLoad(string path, out string error)
    {
        error = null;

        try
        {
            if (!File.Exists(path))
            {
                error = $"No model file at {path}";
                return null;
            }

            MlpModelData data = JsonUtility.FromJson<MlpModelData>(File.ReadAllText(path));
            error = Validate(data);

            return error == null ? new LeadershipMlpModel { Data = data } : null;
        }
        catch (Exception e)
        {
            error = $"Could not read model: {e.Message}";
            return null;
        }
    }

    private static string Validate(MlpModelData data)
    {
        if (data == null || data.layers == null || data.layers.Count == 0) return "Model has no layers.";
        if (data.scalerMean == null || data.scalerScale == null || data.scalerMean.Length != data.scalerScale.Length) return "Scaler is invalid.";
        if (data.inputTransform != "log1p") return $"Unsupported input transform '{data.inputTransform}'.";

        int size = data.scalerMean.Length;

        foreach (MlpLayerData layer in data.layers)
        {
            if (layer.inputs != size) return "Layer sizes do not chain.";
            if (layer.weights == null || layer.weights.Length != layer.inputs * layer.outputs) return "Layer weights have the wrong size.";
            if (layer.biases == null || layer.biases.Length != layer.outputs) return "Layer biases have the wrong size.";
            size = layer.outputs;
        }

        if (data.classes == null || data.classes.Length != size) return "Class count does not match the output layer.";
        return null;
    }

    /// <summary> Returns one probability per entry in Data.classes. </summary>
    public float[] Predict(float[] features)
    {
        if (features == null || features.Length != FeatureCount)
            throw new ArgumentException($"Expected {FeatureCount} features.");

        float[] x = new float[features.Length];
        for (int i = 0; i < x.Length; i++)
        {
            float scale = Data.scalerScale[i] == 0f ? 1f : Data.scalerScale[i];
            x[i] = ((float)Math.Log(1.0 + Math.Max(0f, features[i])) - Data.scalerMean[i]) / scale;
        }

        foreach (MlpLayerData layer in Data.layers)
        {
            float[] y = new float[layer.outputs];

            for (int j = 0; j < layer.outputs; j++)
            {
                float sum = layer.biases[j];
                for (int i = 0; i < layer.inputs; i++)
                    sum += x[i] * layer.weights[i * layer.outputs + j];
                y[j] = sum;
            }

            Activate(y, layer.activation);
            x = y;
        }

        return x;
    }

    private static void Activate(float[] v, string activation)
    {
        switch (activation)
        {
            case "relu":
                for (int i = 0; i < v.Length; i++) v[i] = Mathf.Max(0f, v[i]);
                break;

            case "softmax":
                float max = float.MinValue;
                foreach (float f in v) max = Mathf.Max(max, f);

                float sum = 0f;
                for (int i = 0; i < v.Length; i++) { v[i] = Mathf.Exp(v[i] - max); sum += v[i]; }
                for (int i = 0; i < v.Length; i++) v[i] /= sum;
                break;

            case "logistic":
                for (int i = 0; i < v.Length; i++) v[i] = 1f / (1f + Mathf.Exp(-v[i]));
                break;

            case "tanh":
                for (int i = 0; i < v.Length; i++) v[i] = (float)Math.Tanh(v[i]);
                break;

            case "identity":
                break;

            default:
                throw new NotSupportedException($"Unsupported activation '{activation}'.");
        }
    }
}
