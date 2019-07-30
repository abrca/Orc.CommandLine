using System.Reflection;

//-------------------------------------------------------------

private static readonly Dictionary<string, bool> _dotNetCoreCache = new Dictionary<string, bool>();

//-------------------------------------------------------------

public interface IProcessor
{
    bool HasItems(BuildContext buildContext);

    Task PrepareAsync(BuildContext buildContext);
    Task UpdateInfoAsync(BuildContext buildContext);
    Task BuildAsync(BuildContext buildContext);
    Task PackageAsync(BuildContext buildContext);
    Task DeployAsync(BuildContext buildContext);
    Task FinalizeAsync(BuildContext buildContext);
}

//-------------------------------------------------------------

public abstract class ProcessorBase : IProcessor
{    
    protected readonly ICakeContext CakeContext;

    protected ProcessorBase(ICakeContext cakeContext)
    {
        CakeContext = cakeContext;

        Name = GetProcessorName();
    }

    public string Name { get; private set; }

    protected virtual string GetProcessorName()
    {
        var name = GetType().Name.Replace("Processor", string.Empty);
        return name;
    }

    public abstract bool HasItems(BuildContext buildContext);

    public abstract Task PrepareAsync(BuildContext buildContext);
    public abstract Task UpdateInfoAsync(BuildContext buildContext);
    public abstract Task BuildAsync(BuildContext buildContext);
    public abstract Task PackageAsync(BuildContext buildContext);
    public abstract Task DeployAsync(BuildContext buildContext);
    public abstract Task FinalizeAsync(BuildContext buildContext);
}

//-------------------------------------------------------------

public interface IBuildContext
{
    void Validate();
    void LogStateInfo();
}

//-------------------------------------------------------------

public abstract class BuildContextBase : IBuildContext
{
    private List<IBuildContext> _childContexts;
    private readonly string _contextName;

    protected readonly ICakeContext CakeContext;

    protected BuildContextBase(ICakeContext cakeContext)
    {
        CakeContext = cakeContext;

        _contextName = GetContextName();
    }

    private List<IBuildContext> GetChildContexts()
    {
        var items = _childContexts;
        if (items is null)
        {
            items = new List<IBuildContext>();

            var properties = GetType().GetProperties(BindingFlags.FlattenHierarchy);

            foreach (var property in properties)
            {
                if (property.GetInterfaces().Any(x => x.Type == (typeof(IBuildContext))))
                {
                    items.Add((IBuildContext)property.GetValue(this, null));
                }
            }

            _childContexts = items;

            CakeContext.Information($"Found '{items.Count}' child contexts for '{_contextName}' context");
        }

        return items;
    }

    protected virtual string GetContextName()
    {
        var name = GetType().Name.Replace("Context", string.Empty);
        return name;
    }

    public void Validate()
    {
        CakeContext.Information($"Validating '{_contextName}' context");

        ValidateContext();

        foreach (var childContext in GetChildContexts())
        {
            childContext.Validate();
        }
    }

    protected abstract void ValidateContext();

    public void LogStateInfo()
    {
        LogStateInfoForContext();

        foreach (var childContext in GetChildContexts())
        {
            childContext.LogStateInfo();
        }
    }

    protected abstract void LogStateInfoForContext();
}

//-------------------------------------------------------------

public abstract class BuildContextWithItemsBase : BuildContextBase
{
    protected BuildContextWithItemsBase(ICakeContext cakeContext)
        : base(cakeContext)
    {

    }

    public List<string> Items { get; set; }
}

//-------------------------------------------------------------

public enum TargetType
{
    Unknown,

    Component,

    DockerImage,

    GitHubPages,

    Tool,

    UwpApp,

    VsExtension,

    WebApp,

    WpfApp
}

//-------------------------------------------------------------

private static void LogSeparator(string messageFormat, params object[] args)
{
    Information("");
    Information("--------------------------------------------------------------------------------");
    Information(messageFormat, args);
    Information("--------------------------------------------------------------------------------");
    Information("");
}

//-------------------------------------------------------------

private static void LogSeparator()
{
    Information("");
    Information("--------------------------------------------------------------------------------");
    Information("");
}

//-------------------------------------------------------------

private static string GetTempDirectory(string section, string projectName)
{
    var tempDirectory = Directory(string.Format("./temp/{0}/{1}", section, projectName));

    CreateDirectory(tempDirectory);

    return tempDirectory;
}

//-------------------------------------------------------------

private static List<string> SplitCommaSeparatedList(string value)
{
    return SplitSeparatedList(value, ',');
}

//-------------------------------------------------------------

private static List<string> SplitSeparatedList(string value, params char[] separators)
{
    var list = new List<string>();
            
    if (!string.IsNullOrWhiteSpace(value))
    {
        var splitted = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var split in splitted)
        {
            list.Add(split.Trim());
        }
    }

    return list;
}

//-------------------------------------------------------------

private static void RestoreNuGetPackages(BuildContext buildContext, Cake.Core.IO.FilePath solutionOrProjectFileName)
{
    Information("Restoring packages for {0}", solutionOrProjectFileName);
    
    try
    {
        var nuGetRestoreSettings = new NuGetRestoreSettings
        {
        };

        var sources = SplitSeparatedList(buildContext.General.NuGet.PackageSources, ';');
        if (sources.Count > 0)
        {
            nuGetRestoreSettings.Source = sources;
        }

        NuGetRestore(solutionOrProjectFileName, nuGetRestoreSettings);
    }
    catch (Exception)
    {
        // Ignore
    }
}

//-------------------------------------------------------------

private static void ConfigureMsBuild(BuildContext buildContext, MSBuildSettings msBuildSettings, string projectName, 
    string outputRootDirectory, string action = "build", bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(buildContext, allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        msBuildSettings.ToolPath = toolPath;
    }

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLogger
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(outputRootDirectory, string.Format(@"MsBuild_{0}_{1}.log", projectName, action))
    });

    // Enable for bin logging
    msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    {
        Enabled = true,
        Imports = MSBuildBinaryLogImports.Embed,
        FileName = System.IO.Path.Combine(outputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    };
}

//-------------------------------------------------------------

private static void ConfigureMsBuildForDotNetCore(BuildContext buildContext,DotNetCoreMSBuildSettings msBuildSettings, string projectName, 
    string outputRootDirectory, string action = "build", bool? allowVsPrerelease = null)
{
    var toolPath = GetVisualStudioPath(buildContext, allowVsPrerelease);
    if (!string.IsNullOrWhiteSpace(toolPath))
    {
        msBuildSettings.ToolPath = toolPath;
    }

    // Use as much CPU as possible
    msBuildSettings.MaxCpuCount = 0;
    
    // Enable for file logging
    msBuildSettings.AddFileLogger(new MSBuildFileLoggerSettings
    {
        Verbosity = msBuildSettings.Verbosity,
        //Verbosity = Verbosity.Diagnostic,
        LogFile = System.IO.Path.Combine(outputRootDirectory, string.Format(@"MsBuild_{0}_{1}.log", projectName, action))
    });

    // Enable for bin logging
    //msBuildSettings.BinaryLogger = new MSBuildBinaryLogSettings
    //{
    //    Enabled = true,
    //    Imports = MSBuildBinaryLogImports.Embed,
    //    FileName = System.IO.Path.Combine(OutputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action))
    //};
    
    // Note: this only works for direct .net core msbuild usage, not when this is
    // being wrapped in a tool (such as 'dotnet pack')
    var binLogArgs = string.Format("-bl:\"{0}\";ProjectImports=Embed", 
        System.IO.Path.Combine(outputRootDirectory, string.Format(@"MsBuild_{0}_{1}.binlog", projectName, action)));

    msBuildSettings.ArgumentCustomization = args => args.Append(binLogArgs);
}

//-------------------------------------------------------------

private static string GetVisualStudioDirectory(BuildContext buildContext, bool? allowVsPrerelease = null)
{
    // TODO: Support different editions (e.g. Professional, Enterprise, Community, etc)

    if ((allowVsPrerelease ?? true) && buildContext.General.UseVisualStudioPrerelease)
    {
        Debug("Checking for installation of Visual Studio 2019 preview");

        var pathFor2019Preview = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\";
        if (System.IO.Directory.Exists(pathFor2019Preview))
        {
           Information("Using Visual Studio 2019 preview, note that SonarQube will be disabled since it's not (yet) compatible with VS2019");
           buildContext.SonarQube.IsDisabled  = true;
           return pathFor2019Preview;
        }

        Debug("Checking for installation of Visual Studio 2017 preview");

        var pathFor2017Preview = @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Professional\";
        if (System.IO.Directory.Exists(pathFor2017Preview))
        {
            Information("Using Visual Studio 2017 preview");
            return pathFor2017Preview;
        }
    }
    
    Debug("Checking for installation of Visual Studio 2019");

    var pathFor2019 = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\";
    if (System.IO.Directory.Exists(pathFor2019))
    {
       Information("Using Visual Studio 2019");
       return pathFor2019;
    }

    Debug("Checking for installation of Visual Studio 2017");

    var pathFor2017 = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\";
    if (System.IO.Directory.Exists(pathFor2017))
    {
        Information("Using Visual Studio 2017");
        return pathFor2017;
    }

    // Failed
    return null;
}

//-------------------------------------------------------------

private static string GetVisualStudioPath(BuildContext buildContext, bool? allowVsPrerelease = null)
{
    var potentialPaths = new []
    {
        @"MSBuild\Current\Bin\msbuild.exe",
        @"MSBuild\15.0\Bin\msbuild.exe"
    };

    var directory = GetVisualStudioDirectory(buildContext, allowVsPrerelease);

    foreach (var potentialPath in potentialPaths)
    {
        var pathToCheck = string.Format(@"{0}\{1}", directory, potentialPath);
        if (System.IO.File.Exists(pathToCheck))
        {
            return pathToCheck;
        }
    }

    throw new Exception("Could not find the path to Visual Studio (msbuild.exe)");
}

//-------------------------------------------------------------

private static string GetProjectDirectory(string projectName)
{
    var projectDirectory = string.Format("./src/{0}/", projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private static string GetProjectOutputDirectory(BuildContext buildContext, string projectName)
{
    var projectDirectory = string.Format("{0}/{1}", buildContext.General.OutputRootDirectory, projectName);
    return projectDirectory;
}

//-------------------------------------------------------------

private static string GetProjectFileName(string projectName)
{
    var fileName = string.Format("{0}{1}.csproj", GetProjectDirectory(projectName), projectName);
    return fileName;
}

//-------------------------------------------------------------

private static string GetProjectSlug(string projectName)
{
    var slug = projectName.Replace(".", "").Replace(" ", "");
    return slug;
}

//-------------------------------------------------------------

private static string GetTargetSpecificConfigurationValue(TargetType targetType, string configurationPrefix, string fallbackValue)
{
    // Allow per project overrides via "[configurationPrefix][targetType]"
    var keyToCheck = string.Format("{0}{1}", configurationPrefix, targetType);

    var value = GetBuildServerVariable(keyToCheck, fallbackValue);
    return value;
}

//-------------------------------------------------------------

private static string GetProjectSpecificConfigurationValue(string projectName, string configurationPrefix, string fallbackValue)
{
    // Allow per project overrides via "[configurationPrefix][projectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("{0}{1}", configurationPrefix, slug);

    var value = GetBuildServerVariable(keyToCheck, fallbackValue);
    return value;
}

//-------------------------------------------------------------

private static bool IsDotNetCoreProject(string projectName)
{
    var projectFileName = GetProjectFileName(projectName);

    if (!_dotNetCoreCache.TryGetValue(projectFileName, out var isDotNetCore))
    {
        isDotNetCore = false;

        var lines = System.IO.File.ReadAllLines(projectFileName);
        foreach (var line in lines)
        {
            // Match both *TargetFramework* and *TargetFrameworks* 
            var lowerCase = line.ToLower();
            if (lowerCase.Contains("targetframework"))
            {
                if (lowerCase.Contains("netcore"))
                {
                    isDotNetCore = true;
                    break;
                }
            }
        }

        _dotNetCoreCache[projectFileName] = isDotNetCore;
    }

    return _dotNetCoreCache[projectFileName];
}

//-------------------------------------------------------------

private static bool ShouldProcessProject(BuildContext buildContext, string projectName)
{
    // Includes > Excludes
    var includes = buildContext.General.Includes;
    if (includes.Count > 0)
    {
        var process = includes.Any(x => string.Equals(x, projectName, StringComparison.OrdinalIgnoreCase));

        if (!process)
        {
            Warning("Project '{0}' should not be processed, removing from projects to process", projectName);
        }

        return process;
    }

    var excludes = buildContext.General.Excludes;
    if (excludes.Count > 0)
    {
        var process = !excludes.Any(x => string.Equals(x, projectName, StringComparison.OrdinalIgnoreCase));

        if (!process)
        {
            Warning("Project '{0}' should not be processed, removing from projects to process", projectName);
        }

        return process;
    }

    return true;
}

//-------------------------------------------------------------

private static bool ShouldDeployProject(BuildContext buildContext, string projectName)
{
    // Allow the build server to configure this via "Deploy[ProjectName]"
    var slug = GetProjectSlug(projectName);
    var keyToCheck = string.Format("Deploy{0}", slug);

    var value = GetBuildServerVariable(keyToCheck, "True");
    
    Information("Value for '{0}': {1}", keyToCheck, value);
    
    var shouldDeploy = bool.Parse(value);
    return shouldDeploy;
}