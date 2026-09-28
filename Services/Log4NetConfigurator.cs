using log4net;
using log4net.Config;

namespace SkfProductAssistant.Functions.Services;

public static class Log4NetConfigurator
{
    public static void Configure()
    {
        var configurationPath = Path.Combine(AppContext.BaseDirectory, "log4net.config");
        if (!File.Exists(configurationPath))
        {
            throw new FileNotFoundException("log4net.config was not found.", configurationPath);
        }

        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        GlobalContext.Properties["Application"] = "SkfProductAssistant.Functions";
        XmlConfigurator.Configure(new FileInfo(configurationPath));
    }
}