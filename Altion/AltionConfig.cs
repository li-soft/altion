using System.Reflection;

namespace Altion;

public interface IAltionConfig
{
    public char NewLineSign => '\n';
    
    string TestDataFileName { get; set; }
    string OperativeDictionary { get; set; }
    int TestFileRowsGenerationCount { get; set; }

    public int FilesMergeChunkSize { get; set; }
    public int ReaderBufferSize { get; set; }
    public int WriterBufferSize { get; set; }
    public long ChunkFileSize { get; set; }
}

public class AltionConfig : IAltionConfig
{
    public string TestDataFileName { get; set; }
    public string OperativeDictionary { get; set; }
    public int TestFileRowsGenerationCount { get; set; }
    public int FilesMergeChunkSize { get; set; }
    public int ReaderBufferSize { get; set; }
    public int WriterBufferSize { get; set; }
    public long ChunkFileSize { get; set; }
}
