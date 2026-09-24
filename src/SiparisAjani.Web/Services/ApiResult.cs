using System.Diagnostics.CodeAnalysis;

namespace SiparisAjani.Web.Services;

public enum ApiErrorKind
{
    None,
    /// <summary>API'ye bağlanılamadı (süreç kapalı, adres yanlış).</summary>
    Unreachable,
    /// <summary>API zaman aşımı süresinde yanıt vermedi.</summary>
    Timeout,
    /// <summary>API başarısız bir HTTP durum kodu döndü.</summary>
    HttpError,
    /// <summary>Yanıt beklenen biçimde değildi.</summary>
    InvalidResponse
}

/// <summary>
/// API çağrısının sonucu. Arayüz istisna yakalamaz; <see cref="Error"/> doğrudan kullanıcıya gösterilebilir Türkçe bir cümledir.
/// </summary>
public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiErrorKind errorKind, string error, int? statusCode)
    {
        Value = value;
        ErrorKind = errorKind;
        Error = error;
        StatusCode = statusCode;
    }

    public T? Value { get; }

    public ApiErrorKind ErrorKind { get; }

    /// <summary>Başarılı sonuçta boş metin.</summary>
    public string Error { get; }

    public int? StatusCode { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Value is not null && ErrorKind == ApiErrorKind.None;

    /// <summary>Sunucuya hiç ulaşılamadıysa ya da yanıt gelmediyse true.</summary>
    public bool IsServerDown => ErrorKind is ApiErrorKind.Unreachable or ApiErrorKind.Timeout;

    public static ApiResult<T> Success(T value)
    {
        return new ApiResult<T>(value, ApiErrorKind.None, string.Empty, null);
    }

    public static ApiResult<T> Failure(ApiErrorKind errorKind, string error, int? statusCode = null)
    {
        return new ApiResult<T>(default, errorKind, error, statusCode);
    }
}