﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Services.Inference;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OscCore;

namespace Baballonia.Services;

public class ParameterSenderService : BackgroundService
{
    private readonly OscSendService oscSendService;
    private readonly ILocalSettingsService localSettingsService;
    private readonly ICalibrationService calibrationService;
    private readonly ILogger<ParameterSenderService> logger;

    private string prefix = "";
    private bool sendNativeVrcEyeTracking;
    private readonly ConcurrentQueue<OscMessage> _sendQueue = new();

    // Expression parameter names
    public readonly Dictionary<string, string> EyeExpressionMap = new()
    {
        { "LeftEyeX", "/LeftEyeX" },
        { "LeftEyeY", "/LeftEyeY" },
        { "LeftEyeLid", "/LeftEyeLid" },
        { "RightEyeX", "/RightEyeX" },
        { "RightEyeY", "/RightEyeY" },
        { "RightEyeLid", "/RightEyeLid" },
    };

    public readonly Dictionary<string, string> FaceExpressionMap = new()
    {
        { "CheekPuffLeft", "/cheekPuffLeft" },
        { "CheekPuffRight", "/cheekPuffRight" },
        { "CheekSuckLeft", "/cheekSuckLeft" },
        { "CheekSuckRight", "/cheekSuckRight" },
        { "JawOpen", "/jawOpen" },
        { "JawForward", "/jawForward" },
        { "JawLeft", "/jawLeft" },
        { "JawRight", "/jawRight" },
        { "NoseSneerLeft", "/noseSneerLeft" },
        { "NoseSneerRight", "/noseSneerRight" },
        { "MouthFunnel", "/mouthFunnel" },
        { "MouthPucker", "/mouthPucker" },
        { "MouthLeft", "/mouthLeft" },
        { "MouthRight", "/mouthRight" },
        { "MouthRollUpper", "/mouthRollUpper" },
        { "MouthRollLower", "/mouthRollLower" },
        { "MouthShrugUpper", "/mouthShrugUpper" },
        { "MouthShrugLower", "/mouthShrugLower" },
        { "MouthClose", "/mouthClose" },
        { "MouthSmileLeft", "/mouthSmileLeft" },
        { "MouthSmileRight", "/mouthSmileRight" },
        { "MouthFrownLeft", "/mouthFrownLeft" },
        { "MouthFrownRight", "/mouthFrownRight" },
        { "MouthDimpleLeft", "/mouthDimpleLeft" },
        { "MouthDimpleRight", "/mouthDimpleRight" },
        { "MouthUpperUpLeft", "/mouthUpperUpLeft" },
        { "MouthUpperUpRight", "/mouthUpperUpRight" },
        { "MouthLowerDownLeft", "/mouthLowerDownLeft" },
        { "MouthLowerDownRight", "/mouthLowerDownRight" },
        { "MouthPressLeft", "/mouthPressLeft" },
        { "MouthPressRight", "/mouthPressRight" },
        { "MouthStretchLeft", "/mouthStretchLeft" },
        { "MouthStretchRight", "/mouthStretchRight" },
        { "TongueOut", "/tongueOut" },
        { "TongueUp", "/tongueUp" },
        { "TongueDown", "/tongueDown" },
        { "TongueLeft", "/tongueLeft" },
        { "TongueRight", "/tongueRight" },
        { "TongueRoll", "/tongueRoll" },
        { "TongueBendDown", "/tongueBendDown" },
        { "TongueCurlUp", "/tongueCurlUp" },
        { "TongueSquish", "/tongueSquish" },
        { "TongueFlat", "/tongueFlat" },
        { "TongueTwistLeft", "/tongueTwistLeft" },
        { "TongueTwistRight", "/tongueTwistRight" }
    };

    public ParameterSenderService(
        OscSendService sendService,
        ILocalSettingsService localSettingsService,
        ICalibrationService calibrationService,
        ProcessingLoopService processingLoopService,
        ILogger<ParameterSenderService> logger)
    {
        this.oscSendService = sendService;
        this.localSettingsService = localSettingsService;
        this.calibrationService = calibrationService;
        this.logger = logger;

         processingLoopService.ExpressionChangeEvent += ExpressionUpdateHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting Parameter Sender Service...");
        logger.LogDebug("OSC parameter mapping initialized with {EyeCount} eye expressions and {FaceCount} face expressions",
            EyeExpressionMap.Count, FaceExpressionMap.Count);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                prefix = localSettingsService.ReadSetting<string>("AppSettings_OSCPrefix");
                sendNativeVrcEyeTracking = localSettingsService.ReadSetting<bool>("VRC_UseNativeTracking");
                await SendAndClearQueue(cancellationToken);
                await Task.Delay(10, cancellationToken);
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }

    private void ExpressionUpdateHandler(ProcessingLoopService.Expressions expressions)
    {
        if (expressions.EyeExpression != null)
            ProcessEyeExpressionData(expressions.EyeExpression);
        if (expressions.FaceExpression != null)
            ProcessFaceExpressionData(expressions.FaceExpression);
    }

    private void ProcessEyeExpressionData(float[] expressions)
    {
        if (expressions is null) return;
        if (expressions.Length == 0) return;

        for (int i = 0; i < Math.Min(expressions.Length, EyeExpressionMap.Count); i++)
        {
            var weight = expressions[i];
            var eyeElement = EyeExpressionMap.ElementAt(i);
            var settings = calibrationService.GetExpressionSettings(eyeElement.Key);

            var msg = new OscMessage(prefix + eyeElement.Value,
                weight.Remap(settings.Lower, settings.Upper, settings.Min, settings.Max));
            _sendQueue.Enqueue(msg);
        }

        if (!sendNativeVrcEyeTracking) return;

        ProcessNativeVrcEyeTracking(expressions);
    }

    private void ProcessNativeVrcEyeTracking(float[] expressions)
    {
        var leftEyeX = expressions[0];
        var leftEyeY = expressions[1];
        var leftEyeLid = expressions[2];
        var rightEyeX = expressions[3];
        var rightEyeY = expressions[4];
        var rightEyeLid = expressions[5];

        var leftEyeLidSettings = calibrationService.GetExpressionSettings("LeftEyeLid");
        var rightEyeLidSettings = calibrationService.GetExpressionSettings("RightEyeLid");
        var weightedLeftEyeLid = leftEyeLid.Remap(leftEyeLidSettings.Lower, leftEyeLidSettings.Upper, leftEyeLidSettings.Min, leftEyeLidSettings.Max);
        var weightedRightEyeLid = rightEyeLid.Remap(rightEyeLidSettings.Lower, rightEyeLidSettings.Upper, rightEyeLidSettings.Min, rightEyeLidSettings.Max);
        var averageLid = (weightedLeftEyeLid + weightedRightEyeLid) / 2f;
        _sendQueue.Enqueue(new OscMessage("/tracking/eye/EyesClosedAmount", 1f - Math.Clamp(averageLid, 0f, 1f)));

        // Convert normalized eye positions to angles
        const float maxEyeAngle = 45f;
        leftEyeX *= maxEyeAngle;
        leftEyeY *= -maxEyeAngle; // Negative because Y is inverted (up is negative pitch)
        rightEyeX *= maxEyeAngle;
        rightEyeY *= -maxEyeAngle; // Negative because Y is inverted (up is negative pitch)
        _sendQueue.Enqueue(new OscMessage("/tracking/eye/LeftRightPitchYaw", leftEyeY, rightEyeX, rightEyeY, leftEyeX));
    }

    private void ProcessFaceExpressionData(float[] expressions)
    {
        if (expressions == null) return;
        if (expressions.Length == 0) return;

        for (int i = 0; i < Math.Min(expressions.Length, FaceExpressionMap.Count); i++)
        {
            var weight = expressions[i];
            var faceElement = FaceExpressionMap.ElementAt(i);
            var settings = calibrationService.GetExpressionSettings(faceElement.Key);

            var msg = new OscMessage(prefix + faceElement.Value,
                Math.Clamp(
                    weight.Remap(settings.Lower, settings.Upper, settings.Min, settings.Max),
                    settings.Min,
                    settings.Max));
            _sendQueue.Enqueue(msg);
        }
    }

    private async Task SendAndClearQueue(CancellationToken cancellationToken)
    {
        if (_sendQueue.Count == 0)
            return;

        await oscSendService.Send(_sendQueue.ToArray(), cancellationToken);
        _sendQueue.Clear();
    }
}
