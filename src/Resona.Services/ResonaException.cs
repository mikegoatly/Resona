namespace Resona.Services
{
    [Serializable]
    public class ResonaException : Exception
    {
        public ResonaException() { }
        public ResonaException(string message) : base(message) { }
        public ResonaException(string message, Exception inner) : base(message, inner) { }
    }
}
