namespace EscolaSystemApi.Common;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public int StatusCode { get; }

    private Result(bool isSuccess, T? data, string? error, int statusCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T data) => new(true, data, null, 200);
    public static Result<T> Created(T data) => new(true, data, null, 201);
    public static Result<T> NoContent() => new(true, default, null, 204);
    public static Result<T> NotFound(string error) => new(false, default, error, 404);
    public static Result<T> BadRequest(string error) => new(false, default, error, 400);
    public static Result<T> Forbidden(string error) => new(false, default, error, 403);
    public static Result<T> Conflict(string error) => new(false, default, error, 409);
    public static Result<T> Unauthorized(string error) => new(false, default, error, 401);
}
