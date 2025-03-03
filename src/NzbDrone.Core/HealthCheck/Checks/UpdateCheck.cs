using System;
using System.Collections.Generic;
using System.IO;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ConfigFileSavedEvent))]
    public class UpdateCheck : HealthCheckBase
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly ICheckUpdateService _checkUpdateService;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IOsInfo _osInfo;

        public UpdateCheck(IDiskProvider diskProvider,
                           IAppFolderInfo appFolderInfo,
                           ICheckUpdateService checkUpdateService,
                           IConfigFileProvider configFileProvider,
                           IOsInfo osInfo,
                           ILocalizationService localizationService)
            : base(localizationService)
        {
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
            _checkUpdateService = checkUpdateService;
            _configFileProvider = configFileProvider;
            _osInfo = osInfo;
        }

        public override HealthCheck Check()
        {
            var startupFolder = _appFolderInfo.StartUpFolder;
            var uiFolder = Path.Combine(startupFolder, "UI");

            if (_configFileProvider.UpdateAutomatically &&
                _configFileProvider.UpdateMechanism == UpdateMechanism.BuiltIn &&
                !_osInfo.IsDocker)
            {
                if (OsInfo.IsOsx && startupFolder.GetAncestorFolders().Contains("AppTranslocation"))
                {
                    return new HealthCheck(GetType(),
                        HealthCheckResult.Error,
                        string.Format(_localizationService.GetLocalizedString("UpdateCheckStartupTranslocationMessage"), startupFolder),
                        "#cannot-install-update-because-startup-folder-is-in-an-app-translocation-folder.");
                }

                if (!_diskProvider.FolderWritable(startupFolder))
                {
                    return new HealthCheck(GetType(),
                        HealthCheckResult.Error,
                        string.Format(_localizationService.GetLocalizedString("UpdateCheckStartupNotWritableMessage"), startupFolder, Environment.UserName),
                        "#cannot-install-update-because-startup-folder-is-not-writable-by-the-user");
                }

                if (!_diskProvider.FolderWritable(uiFolder))
                {
                    return new HealthCheck(GetType(),
                        HealthCheckResult.Error,
                        string.Format(_localizationService.GetLocalizedString("UpdateCheckUINotWritableMessage"), uiFolder, Environment.UserName),
                        "#cannot-install-update-because-ui-folder-is-not-writable-by-the-user");
                }
            }

            if (BuildInfo.BuildDateTime < DateTime.UtcNow.AddDays(-14))
            {
                var latestAvailable = _checkUpdateService.AvailableUpdate();

                if (latestAvailable != null)
                {
                    return new HealthCheck(GetType(),
                        HealthCheckResult.Warning,
                        _localizationService.GetLocalizedString("UpdateAvailableHealthCheckMessage", new Dictionary<string, object>
                        {
                            { "version", $"v{latestAvailable.Version}" }
                        }),
                        "#new-update-is-available");
                }
            }

            return new HealthCheck(GetType());
        }
    }
}
