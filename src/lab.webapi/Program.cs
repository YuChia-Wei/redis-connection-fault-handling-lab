namespace lab.webapi;

/// <summary>
/// Program
/// </summary>
public class Program
{
    /// <summary>
    /// Creates the host builder.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
                   .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
                   .UseDefaultServiceProvider((context, options) =>
                   {
                       options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
                       options.ValidateOnBuild = true;
                   });
    }

    /// <summary>
    /// Defines the entry point of the application.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }
}