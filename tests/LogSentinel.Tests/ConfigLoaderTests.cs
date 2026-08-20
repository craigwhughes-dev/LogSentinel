using LogSentinel.Config;

namespace LogSentinel.Tests;

public class ConfigLoaderTests
{
    private const string ValidConfig = """
    {
      "days_to_check": 2,
      "patterns": [ { "name": "error", "regex": "ERROR", "severity": "error" } ],
      "log_dirs": [ { "name": "myapp", "path": "C:\\logs", "codebase_path": "C:\\code" } ]
    }
    """;

    private static string WriteTempConfig(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"logsentinel_test_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_ValidConfig_ParsesExpectedValues()
    {
        var path = WriteTempConfig(ValidConfig);
        try
        {
            var config = ConfigLoader.Load(path);

            Assert.Equal(2, config.DaysToCheck);
            Assert.Single(config.Patterns);
            Assert.Equal("ERROR", config.Patterns[0].Regex);
            Assert.Single(config.LogDirs);
            Assert.Equal("myapp", config.LogDirs[0].Name);
            Assert.Equal("*.log", config.LogDirs[0].FileFilter); // default
            Assert.True(config.Claude.Enabled); // default
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"does_not_exist_{Guid.NewGuid():N}.json");
        Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(missingPath));
    }

    [Fact]
    public void Load_EmptyLogDirs_Throws()
    {
        var content = """
        {
          "patterns": [ { "name": "error", "regex": "ERROR" } ],
          "log_dirs": []
        }
        """;
        var path = WriteTempConfig(content);
        try
        {
            var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(path));
            Assert.Contains("log_dirs", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_EmptyPatterns_Throws()
    {
        var content = """
        {
          "patterns": [],
          "log_dirs": [ { "name": "myapp", "path": "C:\\logs", "codebase_path": "C:\\code" } ]
        }
        """;
        var path = WriteTempConfig(content);
        try
        {
            var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(path));
            Assert.Contains("patterns", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_InvalidRegex_Throws()
    {
        var content = """
        {
          "patterns": [ { "name": "bad", "regex": "(unclosed" } ],
          "log_dirs": [ { "name": "myapp", "path": "C:\\logs", "codebase_path": "C:\\code" } ]
        }
        """;
        var path = WriteTempConfig(content);
        try
        {
            var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(path));
            Assert.Contains("invalid regex", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_DuplicateLogDirNames_Throws()
    {
        var content = """
        {
          "patterns": [ { "name": "error", "regex": "ERROR" } ],
          "log_dirs": [
            { "name": "dup", "path": "C:\\logs1", "codebase_path": "C:\\code1" },
            { "name": "dup", "path": "C:\\logs2", "codebase_path": "C:\\code2" }
          ]
        }
        """;
        var path = WriteTempConfig(content);
        try
        {
            var ex = Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(path));
            Assert.Contains("Duplicate", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingRequiredField_Throws()
    {
        // codebase_path is required but omitted
        var content = """
        {
          "patterns": [ { "name": "error", "regex": "ERROR" } ],
          "log_dirs": [ { "name": "myapp", "path": "C:\\logs" } ]
        }
        """;
        var path = WriteTempConfig(content);
        try
        {
            Assert.Throws<ConfigValidationException>(() => ConfigLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
