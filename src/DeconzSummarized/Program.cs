using DeconzSummarized.Application;
using DeconzSummarized.Config;

var config = AppConfig.Load(AppContext.BaseDirectory);

try
{
    config.Validate();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

return new SummaryPipeline(config).Run();
