using BeNGy.Models;

namespace BeNGy.Services.Interfaces;

public interface IBranchNameGenerator
{
    string Generate(BranchInput input);
}
