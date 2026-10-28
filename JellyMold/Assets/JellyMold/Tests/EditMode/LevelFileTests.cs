using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    /// <summary>Every shipped level must parse, be solvable, and carry a par/move budget matching the solver.</summary>
    public class LevelFileTests
    {
        public const int ExpectedLevelCount = 3;

        private static IEnumerable<string> LevelFiles()
        {
            return Directory.GetFiles(FindLevelDirectory(), "level_*.json").OrderBy(f => f, StringComparer.Ordinal);
        }

        /// <summary>Unity runs tests from the project root; the .NET runner copies levels next to the test binary.</summary>
        private static string FindLevelDirectory()
        {
            string[] candidates =
            {
                Path.Combine(Directory.GetCurrentDirectory(), "Assets/JellyMold/Resources/Levels"),
                Path.Combine(AppContext.BaseDirectory, "Levels"),
            };
            foreach (string c in candidates)
            {
                if (Directory.Exists(c)) return c;
            }
            throw new DirectoryNotFoundException("Level directory not found.");
        }

        [Test]
        public void LevelIdsAreSequential()
        {
            List<LevelData> levels = LevelFiles().Select(f => LevelData.FromJson(File.ReadAllText(f))).ToList();
            Assert.AreEqual(ExpectedLevelCount, levels.Count);
            for (int i = 0; i < levels.Count; i++) Assert.AreEqual(i + 1, levels[i].Id, "file order and ids must match");
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void LevelIsValidAndBudgetMatchesSolver(string file)
        {
            LevelData level = LevelData.FromJson(File.ReadAllText(file));
            ValidationReport report = LevelValidator.Validate(level);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(level.Par, report.Solve.Length, "par");
            Assert.AreEqual(GameConfig.RecommendedMoves(level.Id, report.Solve.Length), level.Moves, "move budget");
            Assert.AreEqual(6, level.Width);
            Assert.AreEqual(6, level.Height);
            Assert.IsNotEmpty(level.Name);
        }
    }
}
