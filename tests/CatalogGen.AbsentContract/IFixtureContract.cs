namespace CatalogGen.AbsentContract;

/// <summary>
/// The contract a fixture type implements so that this assembly's absence makes that type
/// unloadable.
/// </summary>
/// <remarks>
/// Empty on purpose: a type that implements it resolves nothing beyond the interface itself, so the
/// only thing that can fail where this assembly is missing is exactly the thing under test.
/// </remarks>
public interface IFixtureContract
{
}
