namespace Csv
{
    public interface ICsvLine
    {
        string[] Headers { get; }

        string this[string header] { get; }
        string this[int index] { get; }
    }
}