using System.Text.Json;
using System.Text.RegularExpressions;

namespace LogSentinel.Config;

public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(string message) : base(message) { }
}

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ConfigValidationException($"Config file not found: {path}");
        }

        var json = File.ReadAllText(path);
        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException($"Config file is not valid JSON or is missing required fields: {ex.Message}");
        }

        if (config is null)
        {
            throw new ConfigValidationException("Config file deserialized to null.");
        }

        Validate(config);
        return config;
    }

    private static void Validate(AppConfig config)
    {
        if (config.LogDirs.Count == 0)
        {
            throw new ConfigValidationException("Config must define at least one entry in log_dirs.");
        }

        if (config.Patterns.Count == 0)
        {
            throw new ConfigValidationException("Config must define at least one entry in patterns.");
        }

        if (config.DaysToCheck <= 0)
        {
            throw new ConfigValidationException($"days_to_check must be positive, got {config.DaysToCheck}.");
        }

        foreach (var pattern in config.Patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Name))
            {
                throw new ConfigValidationException("Every pattern entry requires a non-empty name.");
            }

            try
            {
                _ = new Regex(pattern.Regex);
            }
            catch (ArgumentException ex)
            {
                throw new ConfigValidationException($"Pattern '{pattern.Name}' has an invalid regex '{pattern.Regex}': {ex.Message}");
            }
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in config.LogDirs)
        {
            if (string.IsNullOrWhiteSpace(dir.Name))
            {
                throw new ConfigValidationException("Every log_dirs entry requires a non-empty name.");
            }

            if (!seenNames.Add(dir.Name))
            {
                throw new ConfigValidationException($"Duplicate log_dirs name: '{dir.Name}'.");
            }

            if (string.IsNullOrWhiteSpace(dir.Path))
            {
                throw new ConfigValidationException($"log_dirs entry '{dir.Name}' requires a non-empty path.");
            }

            if (string.IsNullOrWhiteSpace(dir.CodebasePath))
            {
                throw new ConfigValidationException($"log_dirs entry '{dir.Name}' requires a non-empty codebase_path.");
            }
        }
    }
}
