using System.Collections.Generic;
using FluentValidation;

namespace API.Infrastructure.Validation;

public static class RoleEntriesValidationX
{
    /// <summary>
    /// Validate role entries, e.g. ["role-a", "role-b+role-c"]. Entries are OR'd and the roles within an entry, joined
    /// with "+", are AND'd. Roles can't be empty or contain whitespace or "," (the storage separator), and no entry can
    /// have a subset of another entry's roles, as the larger entry would never be needed.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string[]?> ValidRoleEntries<T>(
        this IRuleBuilder<T, string[]?> ruleBuilder)
        => ruleBuilder.Custom((entries, context) =>
        {
            if (entries == null) return;

            var valid = new List<(string Entry, HashSet<string> Roles)>();
            foreach (var entry in entries)
            {
                // JSON can deserialise null elements despite string[]
                var roles = string.IsNullOrEmpty(entry) ? [string.Empty] : entry.Split('+');
                if (roles.Any(string.IsNullOrEmpty))
                {
                    context.AddFailure($"Role entry '{entry}' contains an empty role");
                }
                else if (roles.Any(r => r.Any(c => char.IsWhiteSpace(c) || c == ',')))
                {
                    context.AddFailure($"Role entry '{entry}' contains whitespace or ','");
                }
                else
                {
                    valid.Add((entry, roles.ToHashSet()));
                }
            }

            for (var i = 0; i < valid.Count; i++)
            for (var j = 0; j < valid.Count; j++)
            {
                if (i == j || !valid[i].Roles.IsSubsetOf(valid[j].Roles)) continue;

                // identical entries are subsets of each other, only report once
                if (valid[i].Roles.SetEquals(valid[j].Roles) && i > j) continue;
                context.AddFailure(
                    $"Role entry '{valid[j].Entry}' is redundant as it contains all roles of '{valid[i].Entry}'");
            }
        });
}
