using Mozdulj.Maui.Services;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
namespace Mozdulj.Maui;

public static class MauiProgram
{
    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton<AppState>();
        return builder.Build();
    }
}
