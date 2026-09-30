using System.Buffers.Text;
using System.Security.Cryptography;

namespace Diva.Api.Features.Orders;

/// <summary>The secret part of a shareable receipt link (/r/{token}).</summary>
public static class PublicToken
{
    public const int ByteLength = 16; // 128 bits: impossible to guess, unlike an order number

    /// <summary>22 URL-safe characters (A-Z, a-z, 0-9, '-' and '_') from a cryptographic random source.</summary>
    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));
}
