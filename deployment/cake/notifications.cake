#l "notifications-msteams.cake"
//#l "notifications-slack.cake"

//-------------------------------------------------------------

public enum NotificationType
{
    Info,

    Error
}

//-------------------------------------------------------------

public class NotificationsContext : ContextBase
{
    public NotificationsContext(ICakeContext cakeContext)
        : base(cakeContext)
    {
    }

    public MsTeamsContext MsTeams { get; set; }
}

//-------------------------------------------------------------

private NotificationsContext InitializeNotificationsContext(ICakeContext cakeContext)
{
    var data = new NotificationsContext(cakeContext)
    {
        MsTeams = InitializeMsTeamsContext(cakeContext)
    };

    return data;
}

//-------------------------------------------------------------

public static async Task NotifyDefaultAsync(BuildContext buildContext, string project, string message, TargetType targetType = TargetType.Unknown)
{
    await NotifyAsync(buildContext, project, message, targetType, NotificationType.Info);
}

//-------------------------------------------------------------

public static async Task NotifyErrorAsync(BuildContext buildContext, string project, string message, TargetType targetType = TargetType.Unknown)
{
    await NotifyAsync(buildContext, project, string.Format("ERROR: {0}", message), targetType, NotificationType.Error);
}

//-------------------------------------------------------------

public static async Task NotifyAsync(BuildContext buildContext, string project, string message, TargetType targetType = TargetType.Unknown, NotificationType notificationType = NotificationType.Info)
{
    await NotifyMsTeamsAsync(buildContext, project, message, targetType, notificationType);

    // TODO: Add more notification systems here such as Slack
}