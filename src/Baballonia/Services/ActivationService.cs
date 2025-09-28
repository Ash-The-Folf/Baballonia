using Avalonia.Logging;
using Baballonia.Contracts;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Baballonia.Services;

public class ActivationService(
    IThemeSelectorService themeSelectorService,
    ILanguageSelectorService languageSelectorService,
    ILogger<ActivationService> logger,
    OpenVRService _openVRService)
    : IActivationService
{
    public ILogger<ActivationService> Logger { get; } = logger;
    public OpenVRService OpenVRService { get; } = _openVRService;

    public void Activate(object activationArgs)
    {
        languageSelectorService.Initialize();
        languageSelectorService.SetRequestedLanguage();
        themeSelectorService.Initialize();
        themeSelectorService.SetRequestedTheme();

        //checking to see if AutoStart has checks pass during service activation
        logger.LogInformation("Configuring OpenVR...");
        if (!_openVRService.AutoStart())
        {
            logger.LogWarning("Failed to configure OpenVR during ActivationService startup. Skipping.");
        }
    }
}
