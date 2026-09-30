using Diva.Api.Auth;

namespace Diva.Tests.Auth;

public class SupabaseAuthOptionsTests
{
    [Theory]
    [InlineData("https://abc.supabase.co")]
    [InlineData("https://abc.supabase.co/")]
    [InlineData("https://abc.supabase.co/rest/v1/")] // what the Supabase dashboard shows for the Data API
    public void Any_form_of_the_project_url_gives_the_same_issuer_and_jwks_address(string url)
    {
        var options = new SupabaseAuthOptions { Url = url };

        Assert.Equal("https://abc.supabase.co", options.BaseUrl);
        Assert.Equal("https://abc.supabase.co/auth/v1", options.Issuer);
        Assert.Equal("https://abc.supabase.co/auth/v1/.well-known/jwks.json", options.JwksUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc.supabase.co")]
    [InlineData("http://abc.supabase.co")]
    public void Urls_that_are_not_absolute_https_are_invalid(string url)
    {
        Assert.False(SupabaseAuthOptions.TryGetBaseUrl(url, out _));
        Assert.Throws<InvalidOperationException>(() => new SupabaseAuthOptions { Url = url }.Issuer);
    }
}
