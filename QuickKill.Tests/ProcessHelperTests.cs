using Xunit;

namespace QuickKill.Tests;

public class ProcessHelperTests
{
    [Theory]
    [InlineData("csrss",    true)]
    [InlineData("wininit",  true)]
    [InlineData("winlogon", true)]
    [InlineData("services", true)]
    [InlineData("lsass",    true)]
    [InlineData("smss",     true)]
    [InlineData("dwm",      true)]
    [InlineData("System",   true)]
    [InlineData("Registry", true)]
    [InlineData("Idle",     true)]
    [InlineData("CSRSS",    true)]
    [InlineData("chrome",   false)]
    [InlineData("notepad",  false)]
    [InlineData("explorer", false)]
    public void IsCriticalProcess_ReturnsExpected(string name, bool expected)
        => Assert.Equal(expected, ProcessHelper.IsCriticalProcess(name));

    [Fact]
    public void GetVisibleProcesses_ContainsNoCriticalProcesses()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var p in ProcessHelper.GetVisibleProcesses())
            Assert.False(ProcessHelper.IsCriticalProcess(p.Name),
                $"Critical process '{p.Name}' leaked into visible list");
    }

    [Fact]
    public void GetVisibleProcesses_AllItemsHaveTitle()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var p in ProcessHelper.GetVisibleProcesses())
            Assert.False(string.IsNullOrWhiteSpace(p.Title));
    }
}
