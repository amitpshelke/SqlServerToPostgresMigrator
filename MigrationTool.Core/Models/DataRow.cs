public sealed class DataRow
{
    public object?[] Values { get; }

    public DataRow(object?[] values)
    {
        Values = values;
    }
}