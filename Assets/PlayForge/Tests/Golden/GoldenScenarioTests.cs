using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FarEmerald.PlayForge.Tests.Golden
{
    /// <summary>
    /// The golden scenarios as Test Runner cases, one per scenario and named after it, so -runTests and CI report each
    /// one (SP-18). The scenarios stay in <see cref="PlayForgeGoldenTests"/>, whose RunAll still runs them
    /// all. The assembly compiles only where tests are included: in a project's Assets, or for a package the project
    /// lists in "testables".
    /// </summary>
    public class GoldenScenarioTests
    {
        private static IEnumerable<TestCaseData> Cases =>
            PlayForgeGoldenTests.ScenarioNames.Select(name => new TestCaseData(name).SetName(name));

        [TestCaseSource(nameof(Cases))]
        public void Scenario(string name)
        {
            var failure = PlayForgeGoldenTests.Run(name);
            if (failure != null) Assert.Fail(failure);
        }
    }
}
