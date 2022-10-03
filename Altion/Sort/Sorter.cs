using Altion.Generate;
using Altion.Shared;
using ShellProgressBar;

namespace Altion.Sort;

internal interface ISorter
{
    Task Sort();
}

internal class Sorter : ISorter
{
    private readonly IAltionConfig _config;
    private readonly IDataFileGenerator _generator;
    
    private FileDataRow[] _unsortedRows;
    
    private const string UnsortedFileExtension = ".altionunsorted";
    private const string SortedFileExtension = ".altionsorted";
    private const string TempFileExtension = ".altiontmp";

    public Sorter(IAltionConfig config, IDataFileGenerator generator)
    {
        _config = config;
        _generator = generator;
        _unsortedRows = Array.Empty<FileDataRow>();
    }

    public async Task Sort()
    {
        var source = await GetSourceStream();
        var destination = GetDestinationStream();

        var files = await SplitFile(source);

        var shouldSkipMerge = files.Count == 1;
        if (shouldSkipMerge)
        {
            var unsortedFilePath = Path.Combine(_config.OperativeDictionary, files.First());
            await SortFile(File.OpenRead(unsortedFilePath), destination);
            return;
        }

        var sortedFiles = await SortFiles(files);
        await MergeFiles(sortedFiles, destination);
    }

    private ProgressBar GetMergeProgressBar(IReadOnlyCollection<string> sortedFiles)
    {
        var size = _config.FilesMergeChunkSize;
        var totalFilesToMerge = sortedFiles.Count;
        var result = sortedFiles.Count / size;
        var done = false;
        while (!done)
        {
            if (result <= 0)
            {
                done = true;
            }

            totalFilesToMerge += result;
            result /= size;
        }
        
        return new ProgressBar(++totalFilesToMerge, "Merging files ...");
    }

    private FileStream GetDestinationStream()
    {
        var destinationPath = Path.Combine(_config.OperativeDictionary, "sorted.altion");
        var destination = File.Exists(destinationPath)
            ? File.OpenWrite(destinationPath)
            : File.Create(destinationPath);
        return destination;
    }

    private async ValueTask<FileStream> GetSourceStream()
    {
        var sourcePath = Path.Combine(_config.OperativeDictionary, _config.TestDataFileName);
        if (!File.Exists(sourcePath))
        {
            Console.WriteLine("No file to be sorted! Generating test file by myself.");
            await _generator.Generate();
        }

        var source = File.OpenRead(sourcePath);
        return source;
    }

    private async Task<IReadOnlyCollection<string>> SplitFile(Stream sourceStream)
    {
        var fileSize = _config.ChunkFileSize;
        var buffer = new byte[fileSize];
        var extraBuffer = new List<byte>();
        var filenames = new List<string>();
        var totalFiles = Math.Ceiling(sourceStream.Length / (double)_config.ChunkFileSize);
        var maxUnsortedRows = 0;

        using var bar = new ProgressBar(Convert.ToInt32(totalFiles), "Splitting big file into the chunks ...");
        await using (sourceStream)
        {
            var currentFile = 0L;
            while (sourceStream.Position < sourceStream.Length)
            {
                var totalRows = 0;
                var runBytesRead = 0;
                while (runBytesRead < fileSize)
                {
                    var value = sourceStream.ReadByte();
                    if (value == -1)
                    {
                        break;
                    }

                    var @byte = (byte)value;
                    buffer[runBytesRead] = @byte;
                    runBytesRead++;
                    if (@byte == _config.NewLineSign)
                    {
                        totalRows++;
                    }
                }

                var extraByte = buffer[fileSize - 1];

                while (extraByte != _config.NewLineSign)
                {
                    var flag = sourceStream.ReadByte();
                    if (flag == -1)
                    {
                        break;
                    }

                    extraByte = (byte)flag;
                    extraBuffer.Add(extraByte);
                }

                var filename = $"{++currentFile}{UnsortedFileExtension}";
                await using var unsortedFile = File.Create(Path.Combine(_config.OperativeDictionary, filename));
                await unsortedFile.WriteAsync(buffer.AsMemory(0, runBytesRead));
                if (extraBuffer.Count > 0)
                {
                    totalRows++;
                    await unsortedFile.WriteAsync(extraBuffer.ToArray().AsMemory(0, extraBuffer.Count));
                }

                if (totalRows > maxUnsortedRows)
                {
                    maxUnsortedRows = totalRows;
                }

                filenames.Add(filename);
                extraBuffer.Clear();
                bar.Tick();
            }

            _unsortedRows = new FileDataRow[maxUnsortedRows];
            
            return filenames;
        }
    }

    private async Task<IReadOnlyList<string>> SortFiles(IReadOnlyCollection<string> unsortedFiles)
    {
        var sortedFiles = new List<string>(unsortedFiles.Count);

        using var bar = new ProgressBar(unsortedFiles.Count, "Soring files ...");
        foreach (var unsortedFile in unsortedFiles)
        {
            var sortedFilename = unsortedFile.Replace(UnsortedFileExtension, SortedFileExtension);
            var unsortedFilePath = Path.Combine(_config.OperativeDictionary, unsortedFile);
            var sortedFilePath = Path.Combine(_config.OperativeDictionary, sortedFilename);
            await SortFile(File.OpenRead(unsortedFilePath), File.OpenWrite(sortedFilePath));
            File.Delete(unsortedFilePath);
            sortedFiles.Add(sortedFilename);
            bar.Tick();
        }

        return sortedFiles;
    }

    private async Task SortFile(Stream unsortedFile, Stream target)
    {
        using var streamReader = new StreamReader(unsortedFile, bufferSize: _config.ReaderBufferSize);
        var counter = 0;
        while (!streamReader.EndOfStream)
        {
            _unsortedRows[counter++] = FileDataRow.FromString((await streamReader.ReadLineAsync())!);
        }

        Array.Sort(_unsortedRows, new FileDataRowComparer());
        await using var streamWriter = new StreamWriter(target, bufferSize: _config.WriterBufferSize);
        foreach (var row in _unsortedRows.Where(x => x != null))
        {
            await streamWriter.WriteLineAsync(row.ToFileRow());
        }

        Array.Clear(_unsortedRows, 0, _unsortedRows.Length);
    }

    private async Task MergeFiles(IReadOnlyList<string> sortedFiles, Stream target)
    {
        using var bar = GetMergeProgressBar(sortedFiles);
        var mergeDone = false;
        while (!mergeDone)
        {
            var runSize = _config.FilesMergeChunkSize;
            var finalRun = sortedFiles.Count <= runSize;

            if (finalRun)
            {
                await Merge(sortedFiles, target, bar);
                return;
            }
            
            var runs = sortedFiles.Chunk(runSize);
            var chunkCounter = 0;
            foreach (var files in runs)
            {
                var outputFilename = $"{++chunkCounter}{SortedFileExtension}{TempFileExtension}";
                if (files.Length == 1)
                {
                    File.Move(GetFullPath(files.First()),
                        GetFullPath(outputFilename.Replace(TempFileExtension, string.Empty)));
                    continue;
                }

                var outputStream = File.OpenWrite(GetFullPath(outputFilename));
                await Merge(files, outputStream, bar);
                
                File.Move(GetFullPath(outputFilename),
                    GetFullPath(outputFilename.Replace(TempFileExtension, string.Empty)), true);
            }

            sortedFiles = Directory.GetFiles(_config.OperativeDictionary, $"*{SortedFileExtension}")
                .OrderBy(x =>
                {
                    var filename = Path.GetFileNameWithoutExtension(x);
                    return int.Parse(filename);
                })
                .ToArray();

            if (sortedFiles.Count > 1)
            {
                continue;
            }

            mergeDone = true;
        }
    }

    private async Task Merge(
        IReadOnlyList<string> filesToMerge,
        Stream outputStream, 
        ProgressBarBase progressBar)
    {
        var (streamReaders, rows) = await InitializeStreamReaders(filesToMerge);
        var finishedStreamReaders = new List<int>(streamReaders.Length);
        var done = false;
        await using var outputWriter = new StreamWriter(outputStream, bufferSize: _config.WriterBufferSize);

        var comparer = new FileDataRowComparer();
        while (!done)
        {
            rows.Sort((row1, row2) => comparer.Compare(row1.Value, row2.Value));
            var valueToWrite = rows[0].Value;
            var streamReaderIndex = rows[0].StreamReader;
            await outputWriter.WriteLineAsync(valueToWrite.ToFileRow());

            if (streamReaders[streamReaderIndex].EndOfStream)
            {
                var indexToRemove = rows.FindIndex(x => x.StreamReader == streamReaderIndex);
                rows.RemoveAt(indexToRemove);
                finishedStreamReaders.Add(streamReaderIndex);
                done = finishedStreamReaders.Count == streamReaders.Length;
                progressBar.Tick();
                continue;
            }

            var value = await streamReaders[streamReaderIndex].ReadLineAsync();
            rows[0] = new Row { Value = FileDataRow.FromString(value!), StreamReader = streamReaderIndex };
        }

        CleanupRun(streamReaders, filesToMerge, progressBar);
    }

    private async Task<(StreamReader[] StreamReaders, List<Row> rows)> InitializeStreamReaders(IReadOnlyList<string> sortedFiles)
    {
        var streamReaders = new StreamReader[sortedFiles.Count];
        var rows = new List<Row>(sortedFiles.Count);
        for (var i = 0; i < sortedFiles.Count; i++)
        {
            var sortedFilePath = GetFullPath(sortedFiles[i]);
            var sortedFileStream = File.OpenRead(sortedFilePath);
            streamReaders[i] = new StreamReader(sortedFileStream, bufferSize: _config.ReaderBufferSize);
            var value = await streamReaders[i].ReadLineAsync();
            var row = new Row
            {
                Value = FileDataRow.FromString(value!),
                StreamReader = i
            };
            rows.Add(row);
        }

        return (streamReaders, rows);
    }

    private void CleanupRun(
        IReadOnlyList<StreamReader> streamReaders, 
        IReadOnlyList<string> filesToMerge,
        ProgressBarBase progressBarBase)
    {
        using var childProgressBar = progressBarBase.Spawn(
            streamReaders.Count,
            "Cleaning after merge phase ...",
            new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGreen,
                ProgressCharacter = '─',
                CollapseWhenFinished = true
            });
        
        for (var i = 0; i < streamReaders.Count; i++)
        {
            streamReaders[i].Dispose();
            var temporaryFilename = $"{filesToMerge[i]}.removal";
            File.Move(GetFullPath(filesToMerge[i]), GetFullPath(temporaryFilename));
            File.Delete(GetFullPath(temporaryFilename));
            childProgressBar.Tick();
        }
    }

    private string GetFullPath(string filename)=> Path.Combine(_config.OperativeDictionary, filename);

    private readonly struct Row
    {
        public FileDataRow Value { get; init; }
        public int StreamReader { get; init; }
    }
}