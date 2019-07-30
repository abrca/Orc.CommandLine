#l "buildserver.cake"

//-------------------------------------------------------------

public class DockerImagesContext : BuildContextWithItemsBase
{
    public string DockerEngineUrl { get; set; }
    public string DockerRegistryUrl { get; set; }
    public string DockerRegistryUserName { get; set; }
    public string DockerRegistryPassword { get; set; }

    protected override void ValidateContext()
    {
    }
    
    protected override void LogStateInfoForContext()
    {
        Information($"Found '{Items.Count}' docker image projects");
    }
}

//-------------------------------------------------------------

private DockerImagesContext InitializeDockerImagesContext(ICakeLog log)
{
    var data = new DockerImagesContext(log)
    {
        Items = DockerImages ?? new List<string>(),
        DockerEngineUrl = GetBuildServerVariable("DockerEngineUrl", showValue: true),
        DockerRegistryUrl = GetBuildServerVariable("DockerRegistryUrl", showValue: true),
        DockerRegistryUserName = GetBuildServerVariable("DockerRegistryUserName", showValue: false),
        DockerRegistryPassword = GetBuildServerVariable("DockerRegistryPassword", showValue: false)
    };

    return data;
}

//-------------------------------------------------------------

List<string> _dockerImages;

public List<string> DockerImages
{
    get
    {
        if (_dockerImages is null)
        {
            _dockerImages = new List<string>();
        }

        return _dockerImages;
    }
}