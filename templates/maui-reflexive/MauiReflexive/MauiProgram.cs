using Microsoft.Extensions.Logging;
using MauiDevFlow.Agent;
using MauiDevFlow.Blazor;
using MauiReflexive.Services;

namespace MauiReflexive;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Copilot services (lightweight no-ops in RELEASE)
        builder.Services.AddSingleton<CopilotService>();
        builder.Services.AddSingleton<CopilotInitService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.AddMauiDevFlowAgent();
        builder.AddMauiBlazorDevFlowTools();

        builder.Services.AddSingleton<RelaunchService>();
        builder.Services.AddSingleton<DevTunnelService>();
        builder.Services.AddSingleton<WsBridgeServer>();
        builder.Services.AddSingleton<WsBridgeClient>();
#endif

        return builder.Build();
    }
}
