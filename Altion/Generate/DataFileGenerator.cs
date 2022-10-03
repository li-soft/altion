using Altion.Shared;
using Bogus;
using Bogus.DataSets;
using ShellProgressBar;
using System.Text;

namespace Altion.Generate;

internal interface IDataFileGenerator
{
    Task Generate();
}

internal class DataFileGenerator : IDataFileGenerator
{
    private readonly IAltionConfig _config;

    public DataFileGenerator(IAltionConfig config)
    {
        _config = config;
    }

    public async Task Generate()
    {
        ManageTempDirectory();

        var path = Path.Combine(_config.OperativeDictionary, _config.TestDataFileName);
        await using var stream = new StreamWriter(path, true);

        using var bar = new ProgressBar(
            _config.TestFileRowsGenerationCount, 
            $"Generating data file with {_config.TestFileRowsGenerationCount} lines ...");

        var cnt = 1;
        var faker = GetConfiguredFaker();
        foreach (var row in faker.GenerateForever())
        {
            await stream.WriteLineAsync(row.ToFileRow());
            
            cnt++;
            bar.Tick();

            if (cnt == _config.TestFileRowsGenerationCount)
            {
                break;
            }
        }

        await stream.FlushAsync();
        
        Console.WriteLine("{0} lines of data was written to the file", _config.TestFileRowsGenerationCount);
    }

    private Faker<FileDataRow> GetConfiguredFaker()
    {
        return new Faker<FileDataRow>()
            .RuleFor(r => r.Number, (f, _) => f.Random.Long(0, _config.TestFileRowsGenerationCount * 12))
            .RuleFor(r => r.Text, (f, r) => r.Number % 10 == 0 
                ? f.Name.FullName(Name.Gender.Female) 
                : f.Name.FirstName(Name.Gender.Male));
    }

    private void ManageTempDirectory()
    {
        if (Directory.Exists(_config.OperativeDictionary))
        {
            Directory.Delete(_config.OperativeDictionary, true);
        }
        
        Directory.CreateDirectory(_config.OperativeDictionary);
    }
}