namespace Mauve.Governance.MSTest.Example;

[TestClass]
public sealed class Test1
{
    [TestMethod]
    [TestProperty("Feature", "FEAT-01")]
    [TestProperty("Requirement", "FEAT-01-001")]
    public void TestMethod1()
    {
    }
}
