using Diva.Api.Features.Customers;

namespace Diva.Tests.Features;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("081234567890", "081234567890")]
    [InlineData("0812-3456-7890", "081234567890")]
    [InlineData(" 0812 3456 7890 ", "081234567890")]
    [InlineData("(021) 555.1234", "0215551234")]
    [InlineData("+62 812-3456-7890", "+6281234567890")]
    [InlineData("12345678", "12345678")]          // 8 digits: shortest allowed
    [InlineData("123456789012345", "123456789012345")] // 15 digits: longest allowed
    public void Valid_numbers_are_normalized(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]           // too short
    [InlineData("1234567890123456")]  // too long
    [InlineData("0812abc7890")]       // letters
    [InlineData("0812+34567890")]     // '+' only allowed at the start
    [InlineData("++6281234567890")]
    [InlineData("٠٨١٢٣٤٥٦٧٨٩٠")]      // non-ASCII digits
    public void Invalid_numbers_are_rejected(string? input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out _));
    }
}
