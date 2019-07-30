#l "vsextensions-variables.cake"

#addin "nuget:?package=Cake.FileHelpers&version=3.0.0"

using System.Xml.Linq;

//-------------------------------------------------------------

public class VsExtensionsProcessor : ProcessorBase
{
    public VsExtensionsProcessor(ICakeLog log)
        : base(log)
    {
        
    }

    public override bool HasItems(BuildContext buildContext)
    {
        return buildContext.VsExtensions.Items.Count > 0;
    }

    public override async Task PrepareAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        // Check whether projects should be processed, `.ToList()` 
        // is required to prevent issues with foreach
        foreach (var vsExtension in buildContext.VsExtensions.Items.ToList())
        {
            if (!ShouldProcessProject(buildContext, vsExtension))
            {
                buildContext.VsExtensions.Items.Remove(vsExtension);
            }
        }        
    }

    public override async Task UpdateInfoAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        // Note: since we can't use prerelease tags in VSIX, we will use the commit count
        // as last part of the version
        var version = string.Format("{0}.{1}", buildContext.General.Version.MajorMinorPatch, buildContext.General.Version.CommitsSinceVersionSource);

        foreach (var vsExtension in buildContext.VsExtensions.Items)
        {
            Information("Updating version for vs extension '{0}'", vsExtension);

            var projectDirectory = GetProjectDirectory(vsExtension);

            // Step 1: update vsix manifest
            var vsixManifestFileName = string.Format("{0}\\source.extension.vsixmanifest", projectDirectory);

            TransformConfig(vsixManifestFileName, new TransformationCollection 
            {
                { "PackageManifest/Metadata/Identity/@Version", version },
                { "PackageManifest/Metadata/Identity/@Publisher", buildContext.VsExtensions.PublisherName }
            });
        }        
    }

    public override async Task BuildAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }
        
        foreach (var vsExtension in buildContext.VsExtensions.Items)
        {
            LogSeparator("Building vs extension '{0}'", vsExtension);

            var projectFileName = GetProjectFileName(vsExtension);
            
            var msBuildSettings = new MSBuildSettings {
                Verbosity = Verbosity.Quiet,
                //Verbosity = Verbosity.Diagnostic,
                ToolVersion = MSBuildToolVersion.Default,
                Configuration = buildContext.General.Solution.ConfigurationName,
                MSBuildPlatform = MSBuildPlatform.x86, // Always require x86, see platform for actual target platform
                PlatformTarget = PlatformTarget.MSIL
            };

            ConfigureMsBuild(msBuildSettings, vsExtension);
            
            // Note: we need to set OverridableOutputPath because we need to be able to respect
            // AppendTargetFrameworkToOutputPath which isn't possible for global properties (which
            // are properties passed in using the command line)
            var outputDirectory = GetProjectOutputDirectory(buildContext, vsExtension);
            Information("Output directory: '{0}'", outputDirectory);

            // Since vs extensions (for now) use the old csproj style, make sure
            // to override the output path as well
            // msBuildSettings.WithProperty("OverridableOutputPath", outputDirectory);
            // msBuildSettings.WithProperty("PackageOutputPath", OutputRootDirectory);
            msBuildSettings.WithProperty("OutputPath", outputDirectory);

            MSBuild(projectFileName, msBuildSettings);
        }       
    }

    public override async Task PackageAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        // No packaging required        
    }

    public override async Task DeployAsync(BuildContext buildContext)
    {
        if (!HasItems(buildContext))
        {
            return;
        }

        var vsixPublisherExeDirectory = string.Format(@"{0}\VSSDK\VisualStudioIntegration\Tools\Bin", GetVisualStudioDirectory());
        var vsixPublisherExeFileName = string.Format(@"{0}\VsixPublisher.exe", vsixPublisherExeDirectory);

        foreach (var vsExtension in buildContext.VsExtensions.Items)
        {
            if (!ShouldDeployProject(vsExtension))
            {
                Information("Vs extension '{0}' should not be deployed", vsExtension);
                continue;
            }

            LogSeparator("Deploying vs extension '{0}'", vsExtension);

            // Step 1: copy the output stuff
            var vsExtensionOutputDirectory = GetProjectOutputDirectory(buildContext, vsExtension);
            var payloadFileName = string.Format(@"{0}\{1}.vsix", vsExtensionOutputDirectory, vsExtension);

            var overviewSourceFileName = string.Format(@"src\{0}\overview.md", vsExtension);
            var overviewTargetFileName = string.Format(@"{0}\overview.md", vsExtensionOutputDirectory);
            CopyFile(overviewSourceFileName, overviewTargetFileName);

            var vsGalleryManifestSourceFileName = string.Format(@"src\{0}\source.extension.vsgallerymanifest", vsExtension);
            var vsGalleryManifestTargetFileName = string.Format(@"{0}\source.extension.vsgallerymanifest", vsExtensionOutputDirectory);
            CopyFile(vsGalleryManifestSourceFileName, vsGalleryManifestTargetFileName);

            // Step 2: update vs gallery manifest
            var fileContents = System.IO.File.ReadAllText(vsGalleryManifestTargetFileName);

            fileContents = fileContents.Replace("[PUBLISHERNAME]", buildContext.VsExtensions.PublisherName);

            System.IO.File.WriteAllText(vsGalleryManifestTargetFileName, fileContents);

            // Step 3: go ahead and publish
            StartProcess(vsixPublisherExeFileName, new ProcessSettings 
            {
                Arguments = new ProcessArgumentBuilder()
                    .Append("publish")
                    .AppendSwitch("-payload", payloadFileName)
                    .AppendSwitch("-publishManifest", vsGalleryManifestTargetFileName)
                    .AppendSwitchSecret("-personalAccessToken", buildContext.VsExtensions.PersonalAccessToken)
            });

            await NotifyAsync(vsExtension, string.Format("Deployed to Visual Studio Gallery"), TargetType.VsExtension);
        }        
    }

    public override async Task FinalizeAsync(BuildContext buildContext)
    {

    }
}
