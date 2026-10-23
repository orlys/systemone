namespace SystemOneTests;

/// <summary>只在指定環境變數有值時執行；否則回報為略過</summary>
public sealed class EnvironmentFactAttribute : FactAttribute
{
    public EnvironmentFactAttribute(string variable)
    {
        if (Environment.GetEnvironmentVariable(variable) is not { Length: > 0 })
        {
            Skip = $"Set {variable} to run this integration test.";
        }
    }
}