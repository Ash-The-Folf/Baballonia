﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baballonia.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Baballonia.Services;

public class DefaultInferenceRunner : IInferenceRunner
{
    private ILogger<DefaultInferenceRunner> _logger;

    private string _inputName;
    private InferenceSession _session;
    public DenseTensor<float> InputTensor;
    public Size InputSize { get; private set; }

    public DefaultInferenceRunner(ILogger<DefaultInferenceRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads/reloads the ONNX model and setups the environment
    /// </summary>
    public void Setup(string modelName, bool useGpu = true)
    {
        SessionOptions sessionOptions = SetupSessionOptions();
        if (useGpu)
            ConfigurePlatformSpecificGpu(sessionOptions);
        else
            sessionOptions.AppendExecutionProvider_CPU();

        _session = new InferenceSession(Path.Combine(AppContext.BaseDirectory, modelName), sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
        var dimensions = _session.InputMetadata.Values.First().Dimensions;
        InputSize = new Size(dimensions[2], dimensions[3]);

        InputTensor = new DenseTensor<float>([1, 1, dimensions[2], dimensions[3]]);

        _logger.LogInformation("{} initialization finished", modelName);
    }

    /// <summary>
    /// Per-platform hardware accel. detection/activation
    /// </summary>
    /// <param name="sessionOptions"></param>
    private void ConfigurePlatformSpecificGpu(SessionOptions sessionOptions)
    {
        // "The Android Neural Networks API (NNAPI) is an Android C API designed for
        // running computationally intensive operations for machine learning on Android devices."
        // It was added in Android 8.1 and will be deprecated in Android 15
        if (OperatingSystem.IsAndroid() &&
            OperatingSystem.IsAndroidVersionAtLeast(8, 1) && // At least 8.1
            !OperatingSystem.IsAndroidVersionAtLeast(15)) // At most 15
        {
            sessionOptions.AppendExecutionProvider_Nnapi();
            _logger.LogInformation("Initialized ExecutionProvider: nnAPI");
            return;
        }

        if (OperatingSystem.IsIOS() ||
            OperatingSystem.IsMacCatalyst() ||
            OperatingSystem.IsMacOS() ||
            OperatingSystem.IsWatchOS() ||
            OperatingSystem.IsTvOS())
        {
            sessionOptions.AppendExecutionProvider_CoreML();
            _logger.LogInformation("Initialized ExecutionProvider: CoreML");
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            // If DirectML is supported on the user's system, try using it first.
            // This has support for both AMD and Nvidia GPUs, and uses less memory in my testing
            try
            {
                sessionOptions.AppendExecutionProvider_DML();
                _logger.LogInformation("Initialized ExecutionProvider: DirectML");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure Gpu.");
                _logger.LogWarning("Failed to create DML Execution Provider on Windows. Falling back to CUDA...");
            }
        }

        // If the user's system does not support DirectML (for whatever reason,
        // it's shipped with Windows 10, version 1903(10.0; Build 18362)+
        // Fallback on good ol' CUDA
        try
        {
            sessionOptions.AppendExecutionProvider_CUDA();
            _logger.LogInformation("Initialized ExecutionProvider: CUDA");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Gpu.");
            _logger.LogWarning("Failed to create CUDA Execution Provider.");
        }

        // And, if CUDA fails (or we have an AMD card)
        // Try one more time with ROCm
        try
        {
            sessionOptions.AppendExecutionProvider_ROCm();
            _logger.LogInformation("Initialized ExecutionProvider: ROCm");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure ROCm.");
            _logger.LogWarning("Failed to create ROCm Execution Provider.");
        }

        _logger.LogWarning("No GPU acceleration will be applied.");
        sessionOptions.AppendExecutionProvider_CPU();
    }

    /// <summary>
    /// Make our SessionOptions *fancy*
    /// </summary>
    /// <returns></returns>
    private SessionOptions SetupSessionOptions()
    {
        // Random environment variable(s) to speed up webcam opening on the MSMF backend.
        // https://github.com/opencv/opencv/issues/17687
        Environment.SetEnvironmentVariable("OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS", "0");
        Environment.SetEnvironmentVariable("OMP_NUM_THREADS", "1");

        // Setup inference backend
        var sessionOptions = new SessionOptions();
        sessionOptions.InterOpNumThreads = 1;
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        // ~3% savings worth ~6ms avg latency. Not noticeable at 60fps?
        sessionOptions.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        sessionOptions.EnableMemoryPattern = true;
        return sessionOptions;
    }

    /// <summary>
    /// Runs inference on current InputTensor
    /// </summary>
    /// <returns></returns>
    public float[] RunInference()
    {
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, InputTensor)
        };

        using var results = _session.Run(inputs);

        var arKitExpressions = results[0].AsEnumerable<float>().ToArray();
        return arKitExpressions;
    }


    public float[]? Run(Mat image)
    {
        if (image.Width != InputSize.Width || image.Height != InputSize.Height)
        {
            _logger.LogError("Wrong dimentions for inference input, Expected: {} {}, Got: {} {}", InputSize.Width,
                InputSize.Height, image.Width, image.Height);
            return null;
        }

        float[] result;
        unsafe
        {
            fixed (float* array = InputTensor.Buffer.Span)
            {
                using var finalMat =
                    Mat.FromPixelData(InputSize.Height, InputSize.Width, MatType.CV_32F, new IntPtr(array));

                Cv2.Resize(image, finalMat, InputSize);
                result = RunInference();
            }
        }

        return result;
    }
}
