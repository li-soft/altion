using CommandLine;

internal class AltiCommandArgs
{
    internal const string GenerateTestFileHelpText =
        "Generate file with test data to be sroted. File will be generated " +
        "under the app location. Please clean the space afterwards by yourself.";
    
    [Option('g', "generate", HelpText = GenerateTestFileHelpText)]
    public bool GenerateTestFile { get; set; }

    internal const string SortHelpText =
        "Sort previously generated test file (see help or run app with 'generate' option)." +
        "Sorted text file will have .srtd extension and will be placed unded the app location." +
        "Please clean the space afterwards by yourself.";
    
    [Option('s', "sort", HelpText = SortHelpText)]
    public bool Sort { get; set; }
}