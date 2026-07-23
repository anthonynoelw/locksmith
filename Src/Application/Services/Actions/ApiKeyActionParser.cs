namespace Application.Services.Actions;

using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Enums;
using Domain.Exceptions;

/// <summary>
/// Parses and validates <see cref="ApiKeyActionEnum"/> values supplied by callers, rejecting anything
/// that is not an exact, defined action. Kept in the application layer so controllers stay free of
/// this validation logic.
/// </summary>
internal static class ApiKeyActionParser
{
    /// <summary>
    /// Resolves an action name to its enum value using an exact, case-insensitive name match.
    /// </summary>
    /// <remarks>
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> is deliberately avoided: it also accepts
    /// numeric strings ("3") and comma-separated name lists ("Write,Delete" ORs to 3 == Execute), which
    /// would let a caller grant an action they never named. Only exact name matches are accepted.
    /// </remarks>
    /// <param name="actionName">The action name to parse.</param>
    /// <returns>The parsed action.</returns>
    /// <exception cref="ValidationException">Thrown when the name does not match a defined action.</exception>
    public static ApiKeyActionEnum Parse(string actionName)
    {
        foreach (ApiKeyActionEnum action in Enum.GetValues<ApiKeyActionEnum>())
        {
            if (string.Equals(action.ToString(), actionName, StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }

        throw new ValidationException(
            "Validation failed.",
            new Dictionary<string, string[]> { { "Action", new[] { $"'{actionName}' is not a valid action." } } });
    }

    /// <summary>
    /// Ensures every value in the set is a defined <see cref="ApiKeyActionEnum"/>.
    /// </summary>
    /// <remarks>
    /// JSON enum binding accepts any integer, so undefined values (e.g. 42) must be rejected here before
    /// they are persisted as granted permissions.
    /// </remarks>
    /// <param name="actions">The actions to validate.</param>
    /// <exception cref="ValidationException">Thrown when any action value is not defined.</exception>
    public static void ValidateDefined(IReadOnlyList<ApiKeyActionEnum> actions)
    {
        string[] invalid = actions
            .Where(a => !Enum.IsDefined(a))
            .Select(a => $"'{(int)a}' is not a valid action.")
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new ValidationException(
                "Validation failed.",
                new Dictionary<string, string[]> { { "Actions", invalid } });
        }
    }
}
