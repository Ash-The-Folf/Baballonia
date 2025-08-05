﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Discord.Commands.Builders;

namespace Baballonia.Services;

public class CalibrationService : ICalibrationService
{
    // Expression parameter names
    private readonly Dictionary<string, string> _eyeExpressionMap = new()
    {
        { "LeftEyeX", "/LeftEyeX" },
        { "LeftEyeY", "/LeftEyeY" },
        { "RightEyeX", "/RightEyeX" },
        { "RightEyeY", "/RightEyeY" },
        { "LeftEyeLid", "/LeftEyeLid" },
        { "RightEyeLid", "/RightEyeLid" },
        { "BrowRaise", "/BrowRaise" },
        { "BrowAngry", "/BrowAngry" },
        { "EyeWiden", "/EyeWiden" },
        { "EyeSquint", "/EyeSquint" },
        { "EyeDilate", "/EyeDilate" },
    };

    private readonly Dictionary<string, string> _faceExpressionMap = new()
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

    private readonly ConcurrentDictionary<string, CalibrationParameter> _expressionSettings = new();

    private readonly ILocalSettingsService _localSettingsService;

    private readonly Task _isInitializedTask;
    public CalibrationService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;

        _isInitializedTask = LoadAsync();
    }

    private class CalibrationParameter
    {
        public float Lower { get; set; } = 0;
        public float Upper { get; set; } = 1;

        public CalibrationParameter() {}
        public CalibrationParameter(float lower, float upper)
        {
            Lower = lower;
            Upper = upper;
        }
    }

    public async Task SetExpression(string expression, float value)
    {
        await _isInitializedTask;

        if (string.IsNullOrEmpty(expression))
            return;

        if (!expression.EndsWith("Lower") && !expression.EndsWith("Upper")) return;

        var isUpper = expression.EndsWith("Upper");
        var parameterName = expression[..^5]; // Remove "Upper"/"Lower", both 5 letters in size :3

        _expressionSettings.TryGetValue(parameterName, out var currentSettings);

        var lower = isUpper ? currentSettings.Lower : value;
        var upper = isUpper ? value : currentSettings.Upper;

        var param = new CalibrationParameter(lower, upper);
        _expressionSettings[parameterName] = param;
        await SaveAsync();
    }

    public (float Lower, float Upper) GetExpressionSettings(string parameterName)
    {
        return _expressionSettings.TryGetValue(parameterName, out var settings) ? (settings.Lower, settings.Upper ): (0f, 1f);
    }

    public async Task<float> GetExpressionSetting(string expression)
    {
        await _isInitializedTask;

        if (!expression.EndsWith("Lower") && !expression.EndsWith("Upper")) return 0;

        var isUpper = expression.EndsWith("Upper");
        var parameterName = expression[..^5]; // Remove "Upper"/"Lower", both 5 letters in size :3

        _expressionSettings.TryGetValue(parameterName, out var currentSettings);

        if (currentSettings == null)
            return 0;

        return isUpper ? currentSettings.Upper : currentSettings.Lower;
    }

    private async Task SaveAsync()
    {
        await _isInitializedTask;
        await _localSettingsService.SaveSettingAsync("CalibrationParams", _expressionSettings);
    }

    private async Task LoadAsync()
    {
        var parameters = await _localSettingsService.ReadSettingAsync<ConcurrentDictionary<string, CalibrationParameter>?>("CalibrationParams");
        _expressionSettings.Clear();
        var allParameterNames = _eyeExpressionMap.Keys.Concat(_faceExpressionMap.Keys);
        if(parameters == null)
            foreach (var parameterName in allParameterNames)
            {
                _expressionSettings[parameterName] = new CalibrationParameter();
            }
        else
            foreach (var parameterName in allParameterNames)
            {
                var param = parameters.GetValueOrDefault(parameterName);
                _expressionSettings[parameterName] = param ?? new CalibrationParameter();
            }
    }

    public async Task ResetValues()
    {
        await _isInitializedTask;

        foreach (var parameter in _expressionSettings.Values)
        {
            parameter.Lower = 0;
            parameter.Upper = 1;
        }
        await SaveAsync();
    }

    public async Task ResetMinimums()
    {
        await _isInitializedTask;
        foreach (var parameter in _expressionSettings.Values)
        {
            parameter.Lower = 0;
        }
        await SaveAsync();
    }

    public async Task ResetMaximums()
    {
        await _isInitializedTask;
        foreach (var parameter in _expressionSettings.Values)
        {
            parameter.Upper = 1;
        }
        await SaveAsync();
    }
}
