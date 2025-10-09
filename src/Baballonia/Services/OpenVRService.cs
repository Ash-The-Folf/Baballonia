using Avalonia;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using Valve.VR;

namespace Baballonia.Services
{
   public class OpenVRService
    {
        //app key needed for vrmanifest
        private string application_key = "projectbabble.Baballonia";
        private CVRSystem _system;
        private readonly ILogger<OpenVRService> _logger;

        public OpenVRService(ILogger<OpenVRService> logger)
        {
            _logger = logger;
        }

        public bool IsAutoStartReady { get; private set; }

        //AutoStart function
        public bool AutoStart()
        {
            //checking if SteamVR is open in the background
            EVRInitError error = EVRInitError.None;
            _system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

            if (error != EVRInitError.None)
            {
                _logger.LogWarning("Failed to Enable SteamVR AutoStart: {0}", error);
                IsAutoStartReady = false;
                return IsAutoStartReady;
            }

            //trying to check for and find the manifest.vrmanifest file using the exe's directory
            string? fullManifestPath = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (fullManifestPath == null)
            {
                throw new Exception("Can not find the executable Path");
            }
            var VRManifestPath = Path.GetFullPath(Path.Combine(fullManifestPath, "manifest.vrmanifest"));

            //Checking if the manifest is registered and if anything went wrong
            var VRManifestRegResult = OpenVR.Applications.AddApplicationManifest(VRManifestPath, false);
            if(VRManifestRegResult != EVRApplicationError.None)
            {
                _logger.LogWarning("Failed to register vrmanifest: {0}", VRManifestRegResult);
                IsAutoStartReady = false;
                return IsAutoStartReady;
            }
            //checking if the application in the vrmanifest is valid
            var ApplicationCheck = OpenVR.Applications.IsApplicationInstalled(application_key);
            _logger.LogDebug("checking for application {0}", ApplicationCheck);

            _logger.LogInformation("Successfully Added to SteamVR startup apps");

            IsAutoStartReady = true;
            return IsAutoStartReady;
        }

        //checking to see if autostart is ready
        public void CheckIfReadyIfIsnt()
        {
            if (!IsAutoStartReady)
            {
                try {
                    AutoStart();
               } catch (Exception e) {
                    _logger.LogWarning("dll not found! Your current OS might not be supported for SteamVR AutoStart", e);

                }
            }
        }

        //bool for checking, getting, and setting the application key for auto launch
        public bool SteamvrAutoStart
        {
            get => IsAutoStartReady && OpenVR.Applications.GetApplicationAutoLaunch(application_key);
            set
            {
                if (!IsAutoStartReady && !AutoStart())
                {
                    _logger.LogError("Failed to change SteamVR AutoStart setting. OpenVR could not be Configured.");
                    return;
                }

                var SetAutoStartResult = OpenVR.Applications.SetApplicationAutoLaunch(application_key, value);
                if (SetAutoStartResult != EVRApplicationError.None)
                {
                    _logger.LogError("Failed to set SteamVR Auto Start: {0}", SetAutoStartResult);
                }
            }
        }
    }
}
