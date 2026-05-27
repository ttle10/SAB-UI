namespace SystemeAideBasculement.Models
{

    public interface ICustomMessage
    {
        string DisplayMessage { get; }
        ICustomMessage ToObject();
    }

    public class InfoException : Exception, ICustomMessage
    {
        public InfoException(string message) : base(message) { }
        public string DisplayMessage => base.Message;
        public ICustomMessage ToObject() => new InfoException(base.Message);
    }

    public class WarningException : Exception, ICustomMessage
    {
        public WarningException(string message) : base(message) { }
        public string DisplayMessage => base.Message;
        public ICustomMessage ToObject() => new WarningException(base.Message);
    }

    public class ErrorException : Exception, ICustomMessage 
    {
        public ErrorException(string message) : base(message) { }
        public string DisplayMessage => base.Message;
        public ICustomMessage ToObject() => new ErrorException(base.Message);
    }
}
