using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = false)]

// Collection definition for tests that redirect Console.Out to prevent interference
[CollectionDefinition("ConsoleOutputTests", DisableParallelization = true)]
public class ConsoleOutputTestsCollection
{
}