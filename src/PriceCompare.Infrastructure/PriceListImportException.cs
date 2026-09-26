namespace PriceCompare.Infrastructure;

public sealed class PriceListImportException : Exception
{
    public PriceListImportException(string message) : base(message)
    {
    }

    public PriceListImportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
