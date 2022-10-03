namespace Altion.Shared;

public class FileDataRow
{
    public long Number { get; set; }
    public string Text { get; set; } = null!;

    public static FileDataRow FromString(string value)
    {
        var split = value.Split(". ");
        return new FileDataRow
            {
                Number = long.Parse(split[0]),
                Text = split[1]
            };
    }

    public ReadOnlyMemory<char> ToFileRow() => $"{Number}. {Text}".AsMemory();
}
