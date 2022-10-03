using Altion.Shared;

namespace Altion.Sort;

public class FileDataRowComparer : IComparer<FileDataRow>
{
    public int Compare(FileDataRow? left, FileDataRow? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (ReferenceEquals(null, right))
        {
            return 1;
        }

        if (ReferenceEquals(null, left))
        {
            return -1;
        }

        var textComparison = string.Compare(left.Text, right.Text, StringComparison.Ordinal);
        return textComparison != 0 ? textComparison : left.Number.CompareTo(right.Number);
    }
}