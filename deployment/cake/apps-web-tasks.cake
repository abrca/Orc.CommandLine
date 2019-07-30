#pragma warning disable 1998

#l "apps-web-variables.cake"
#l "lib-octopusdeploy.cake"

#addin "nuget:?package=MagicChunks&version=2.0.0.119"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=Microsoft.Azure.KeyVault.Core&version=1.0.0"
#addin "nuget:?package=WindowsAzure.Storage&version=9.1.1"

//-------------------------------------------------------------

public class WebProcessor : ProcessorBase
{
    public WebProcessor(ICakeLog log)
        : base(log)
    {
        
    }

    public override bool HasItems(BuildContext buildContext)
    {
        return buildContext.Web.Items.Count > 0;
    }

    public override async Task PrepareAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var webApp in buildContext.Web.Items.ToList())
        {
            if (!ShouldProcessProject(buildContext, webApp))
            {
                WebApps.Remove(webApp);
            }
        }
    }

    public override async Task UpdateInfoAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        foreach (var webApp in buildContext.Web.Items)
        {
            Information("Updating version for web app '{0}'", webApp);

            var projectFileName = GetProjectFileName(webApp);

            TransformConfig(projectFileName, new TransformationCollection 
            {
                { "Project/PropertyGroup/PackageVersion", buildContext.General.Version.NuGet }
            });
        }
    }

    public override async Task BuildAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        foreach (var webApp in buildContext.Web.Items)
        {
            LogSeparator("Building web app '{0}'", webApp);

            var projectFileName = GetProjectFileName(webApp);
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet, // Verbosity.Diagnostic
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = buildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(msBuildSettings, webApp);

            // Always disable SourceLink
            msBuildSettings.WithProperty("EnableSourceLink", "false");

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = string.Format("{0}/{1}/", buildContext.General.OutputRootDirectory, webApp);
            Information("Output directory: '{0}'", outputDirectory);
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", buildContext.General.OutputRootDirectory);

            // TODO: Enable GitLink / SourceLink, see RepositoryUrl, RepositoryBranchName, RepositoryCommitId variables

            MSBuild(projectFileName, msBuildSettings);
        }
    }

    public override async Task PackageAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        // For package documentation using Octopus Deploy, see https://octopus.com/docs/deployment-examples/deploying-asp.net-core-web-applications
        
        foreach (var webApp in buildContext.Web.Items)
        {
            LogSeparator("Packaging web app '{0}'", webApp);

            var projectFileName = string.Format("./src/{0}/{0}.csproj", webApp);

            var outputDirectory = string.Format("{0}/{1}/", buildContext.General.OutputRootDirectory, webApp);
            Information("Output directory: '{0}'", outputDirectory);

            Information("1) Using 'dotnet publish' to package '{0}'", webApp);

            var msBuildSettings = new DotNetCoreMSBuildSettings();

            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            msBuildSettings.WithProperty("PackageOutputPath", outputDirectory);
            msBuildSettings.WithProperty("ConfigurationName", buildContext.General.Solution.ConfigurationName);
            msBuildSettings.WithProperty("PackageVersion", buildContext.General.Version.NuGet);

            var publishSettings = new DotNetCorePublishSettings
            {
                MSBuildSettings = msBuildSettings,
                OutputDirectory = outputDirectory,
                Configuration = buildContext.General.Solution.ConfigurationName
            };

            DotNetCorePublish(projectFileName, publishSettings);
            
            Information("2) Using 'octo pack' to package '{0}'", webApp);

            var toolSettings = new DotNetCoreToolSettings
            {
            };

            var octoPackCommand = string.Format("--id {0} --version {1} --basePath {0}", webApp, buildContext.General.Version.NuGet);
            DotNetCoreTool(outputDirectory, "octo pack", octoPackCommand, toolSettings);
            
            LogSeparator();
        }
    }

    public override async Task DeployAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        foreach (var webApp in buildContext.Web.Items)
        {
            if (!ShouldDeployProject(webApp))
            {
                Information("Web app '{0}' should not be deployed", webApp);
                continue;
            }

            LogSeparator("Deploying web app '{0}'", webApp);

            var packageToPush = string.Format("{0}/{1}.{2}.nupkg", buildContext.General.OutputRootDirectory, webApp, buildContext.General.Version.NuGet);
            var octopusRepositoryUrl = GetOctopusRepositoryUrl(webApp);
            var octopusRepositoryApiKey = GetOctopusRepositoryApiKey(webApp);
            var octopusDeploymentTarget = GetOctopusDeploymentTarget(webApp);

            Information("1) Pushing Octopus package");

            OctoPush(octopusRepositoryUrl, octopusRepositoryApiKey, packageToPush, new OctopusPushSettings
            {
                ReplaceExisting = true,
            });

            Information("2) Creating release '{0}' in Octopus Deploy", buildContext.General.Version.NuGet);

            OctoCreateRelease(webApp, new CreateReleaseSettings 
            {
                Server = octopusRepositoryUrl,
                ApiKey = octopusRepositoryApiKey,
                ReleaseNumber = buildContext.General.Version.NuGet,
                DefaultPackageVersion = buildContext.General.Version.NuGet,
                IgnoreExisting = true
            });

            Information("3) Deploying release '{0}'", VersionNuGet);

            OctoDeployRelease(octopusRepositoryUrl, octopusRepositoryApiKey, webApp, octopusDeploymentTarget, 
                buildContext.General.Version.NuGet, new OctopusDeployReleaseDeploymentSettings
            {
                ShowProgress = true,
                WaitForDeployment = true,
                DeploymentTimeout = TimeSpan.FromMinutes(5),
                CancelOnTimeout = true,
                GuidedFailure = true,
                Force = true,
                NoRawLog = true,
            });

            await NotifyAsync(webApp, string.Format("Deployed to Octopus Deploy"), TargetType.WebApp);
        }
    }

    public override async Task FinalizeAsync(BuildContext buildContext)
    {

    }
}