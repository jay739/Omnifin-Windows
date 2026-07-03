using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class ServerConfigSectionMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("advanced")]
    public bool Advanced { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    [JsonPropertyName("depends_true")]
    public string DependsTrue { get; set; } = string.Empty;

    [JsonPropertyName("depends_false")]
    public string DependsFalse { get; set; } = string.Empty;

    [JsonPropertyName("wiki_link")]
    public string WikiLink { get; set; } = string.Empty;
}

/// <summary>
/// An Option is a 2-element string array: [value, display_name].
/// </summary>
public sealed class ServerConfigOption
{
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ServerConfigSetting
{
    [JsonPropertyName("setting")]
    public string Setting { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("requires_restart")]
    public bool RequiresRestart { get; set; }

    [JsonPropertyName("advanced")]
    public bool Advanced { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>
    /// Options arrive as an array of 2-element string arrays: [[value, display], ...].
    /// We deserialize them as List of List of string and convert in code.
    /// </summary>
    [JsonPropertyName("options")]
    public List<List<string>>? Options { get; set; }

    [JsonPropertyName("depends_true")]
    public string DependsTrue { get; set; } = string.Empty;

    [JsonPropertyName("depends_false")]
    public string DependsFalse { get; set; } = string.Empty;

    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; set; }

    [JsonPropertyName("style")]
    public string Style { get; set; } = string.Empty;

    [JsonPropertyName("wiki_link")]
    public string WikiLink { get; set; } = string.Empty;
}

public sealed class ServerConfigSection
{
    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("meta")]
    public ServerConfigSectionMeta Meta { get; set; } = new();

    [JsonPropertyName("settings")]
    public List<ServerConfigSetting> Settings { get; set; } = [];
}

public sealed class ServerConfigMember
{
    [JsonPropertyName("group")]
    public string Group { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;
}

public sealed class ServerConfigGroup
{
    [JsonPropertyName("group")]
    public string GroupKey { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ServerConfigMember> Members { get; set; } = [];
}

public sealed class GetServerConfigResponse
{
    [JsonPropertyName("sections")]
    public List<ServerConfigSection> Sections { get; set; } = [];

    [JsonPropertyName("groups")]
    public List<ServerConfigGroup> Groups { get; set; } = [];

    [JsonPropertyName("order")]
    public List<ServerConfigMember>? Order { get; set; }
}
