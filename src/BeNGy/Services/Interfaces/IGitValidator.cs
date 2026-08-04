using BeNGy.Models;

namespace BeNGy.Services.Interfaces;

public interface IGitValidator
{
    /// <summary>
    /// Validates a branch name against git naming rules.
    /// </summary>
    /// <param name="branchName">The branch name to validate.</param>
    /// <returns>A ValidationResult indicating validity and any invalid characters found.</returns>
    ValidationResult Validate(string branchName);
}
