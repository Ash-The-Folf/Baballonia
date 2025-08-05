﻿using System.Collections.Concurrent;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Baballonia.Services;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Baballonia.Tests.Services;

[TestClass]
[TestSubject(typeof(CalibrationService))]
public class CalibrationServiceTest
{
    private Mock<ILocalSettingsService> _localSettingsMock;
    private CalibrationService _calibrationService;

    [TestInitialize]
    public void Setup()
    {
        _localSettingsMock = new Mock<ILocalSettingsService>();
        _localSettingsMock
            .Setup(x => x.ReadSettingAsync<ConcurrentDictionary<string, object>>("CalibrationParams", null, false))!
            .ReturnsAsync((ConcurrentDictionary<string, object>?)null); // simulate empty storage

        _calibrationService = new CalibrationService(_localSettingsMock.Object);
    }


    [TestMethod]
    public async Task SetExpression_SetsCorrectLowerAndUpperValues()
    {
        var expression = "JawOpenLower";
        var expectedLower = 0.3f;

        await _calibrationService.SetExpression(expression, expectedLower);
        var result = _calibrationService.GetExpressionSettings("JawOpen");

        Assert.AreEqual(expectedLower, result.Lower);
        Assert.AreEqual(1.0f, result.Upper); // default upper
    }

    [TestMethod]
    public async Task SetExpression_UpperValueOverridesCorrectly()
    {
        await _calibrationService.SetExpression("JawOpenLower", 0.2f);
        await _calibrationService.SetExpression("JawOpenUpper", 0.8f);

        var result = _calibrationService.GetExpressionSettings("JawOpen");

        Assert.AreEqual(0.2f, result.Lower);
        Assert.AreEqual(0.8f, result.Upper);
    }

    [TestMethod]
    public async Task ResetValues_ResetsAllToDefaults()
    {
        await _calibrationService.SetExpression("JawOpenLower", 0.5f);
        await _calibrationService.SetExpression("JawOpenUpper", 0.6f);

        await _calibrationService.ResetValues();

        var result = _calibrationService.GetExpressionSettings("JawOpen");

        Assert.AreEqual(0f, result.Lower);
        Assert.AreEqual(1f, result.Upper);
    }
}
