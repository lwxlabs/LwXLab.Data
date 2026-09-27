using System;
using LwXLab.Data;
using Xunit;

namespace LwXLab.Data.Tests;

public sealed class CoreTests
{
    [Theory]
    [InlineData("torser")]
    [InlineData("spotflow_v2")]
    [InlineData("_internal")]
    public void DatabaseIdentifier_AcceptsSafeNames(string value)
    {
        Assert.Equal(value, DatabaseIdentifier.Validate(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad-name")]
    [InlineData("bad name")]
    [InlineData("1starts_with_number")]
    public void DatabaseIdentifier_RejectsUnsafeNames(string value)
    {
        Assert.Throws<ArgumentException>(() => DatabaseIdentifier.Validate(value));
    }
}
