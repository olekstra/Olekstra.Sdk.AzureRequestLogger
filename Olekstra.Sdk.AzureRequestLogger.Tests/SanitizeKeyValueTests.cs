namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using Xunit;

    public class SanitizeKeyValueTests
    {
        [Theory]
        [InlineData("abc123DEF", "abc123DEF")]
        [InlineData("abc123_-=DEF", "abc123_-=DEF")]
        [InlineData("abc\\123/DEF", "abc_123_DEF")]
        [InlineData("abc#~+123", "abc_~_123")]
        [InlineData("abc\t\r\n123", "abc___123")]
        public void ItWorks(string value, string sanitizedValue)
        {
            Assert.Equal(sanitizedValue, LogService.SanitizeKeyValue(value), StringComparer.Ordinal);
        }
    }
}
