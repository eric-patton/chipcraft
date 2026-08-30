namespace ChipCraft.Engine.Composition;

public record StemDefinition(string Name, IReadOnlyList<ChannelRole> Roles);

public static class StemLayoutLibrary
{
    private static readonly StemDefinition[] DefaultDefinitions =
    [
        new("lead", [ChannelRole.Lead]),
        new("rhythm", [ChannelRole.Bass, ChannelRole.Drums]),
        new("harmony", [ChannelRole.Harmony, ChannelRole.PadLow, ChannelRole.PadHigh])
    ];

    public static IReadOnlyList<StemDefinition> Resolve(IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        return DefaultDefinitions
            .Where(definition => assignments.Any(assignment => definition.Roles.Contains(assignment.Role)))
            .ToArray();
    }

    public static IReadOnlyList<int> ResolveChannels(StemDefinition definition, IReadOnlyList<ChannelRoleAssignment> assignments)
    {
        return assignments
            .Where(assignment => definition.Roles.Contains(assignment.Role))
            .Select(assignment => assignment.Channel)
            .Distinct()
            .OrderBy(channel => channel)
            .ToArray();
    }
}
