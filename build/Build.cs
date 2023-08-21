using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;

[GitHubActions(
    "ci-pipeline",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] {"main"},
    InvokedTargets = new[] { nameof(PublishLinux), nameof(PublishWin64) })]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    private const string LeaderBoardGen = "CapLeaderBoardGen.csproj";

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    AbsolutePath PublishFolder = RootDirectory / "publish";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetClean(_ =>
                _.SetProject(Solution.CapLeaderboardGen)
            );
            PublishFolder.DeleteDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(_ =>
                _.SetProjectFile(Solution.CapLeaderboardGen)
            );
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(_ =>
                _.SetProjectFile(Solution.CapLeaderboardGen)
            );
        });

    Target PublishWin64 => _ =>
    {
        
        return _
                .DependsOn(Restore)
                .Executes(() =>
                {
                    DotNetTasks.DotNetPublish(_ =>
                        _.SetConfiguration(Configuration.Release)
                        .EnableContinuousIntegrationBuild()
                        .EnableSelfContained()
                        .EnablePublishReadyToRun()
                        .EnablePublishSingleFile()
                        .SetOutput(PublishFolder / "win")
                        .SetRuntime("win-x64")
                    );
                }).Produces(PublishFolder / "win");
    };

    Target PublishLinux => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetPublish(_ =>
                _.SetConfiguration(Configuration.Release)
                .EnableContinuousIntegrationBuild()
                .EnableSelfContained()
                .EnablePublishReadyToRun()
                .EnablePublishSingleFile()
                .EnableSelfContained()
                .SetOutput(PublishFolder / "linux")
                .SetRuntime("linux-x64"));
        }).Produces(PublishFolder / "linux");
}
