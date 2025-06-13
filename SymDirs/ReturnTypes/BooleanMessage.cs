namespace SymDirs.ReturnTypes;

public class BooleanMessage
{
    public string Message { get; set; }
    public bool Success { get; set; }
    
    public BooleanMessage(string message, bool success)
    {
        Message = message;
        Success = success;
    }
    
    public BooleanMessage<T> WithData<T>(T? data) {
        return new BooleanMessage<T>(Message, Success, data);
    }
}

public class BooleanMessage<T> : BooleanMessage
{
    public T? Data { get; set; }
    
    public BooleanMessage(string message, bool success, T? data) : base(message, success)
    {
        Data = data;
    }
}