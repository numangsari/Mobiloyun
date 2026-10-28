using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    public class DataTests
    {
        [Test]
        public void LevelJsonRoundTrips()
        {
            var level = new LevelData
            {
                Id = 7,
                Name = "Çifte \"dolum\"",
                Moves = 6,
                Par = 4,
                Tutorial = "Satırı kaydır",
                Hint = true,
                Grid = new[] { "r.R", "#t.", "..T" },
            };
            LevelData back = LevelData.FromJson(level.ToJson());
            Assert.AreEqual(level.Id, back.Id);
            Assert.AreEqual(level.Name, back.Name);
            Assert.AreEqual(level.Moves, back.Moves);
            Assert.AreEqual(level.Par, back.Par);
            Assert.AreEqual(level.Tutorial, back.Tutorial);
            Assert.AreEqual(level.Hint, back.Hint);
            Assert.AreEqual(level.Grid, back.Grid);
        }

        [Test]
        public void ParserSeparatesFloorMoldAndJellyLayers()
        {
            BoardState s = TestBoards.State("rT#");
            BoardLayout l = s.Layout;
            Assert.AreEqual(TileType.Floor, l.GetTile(0));
            Assert.AreEqual(JellyColor.Coral, s.GetJelly(0));
            Assert.AreEqual(TileType.Mold, l.GetTile(1));
            Assert.AreEqual(JellyColor.Turquoise, l.GetMoldColor(1));
            Assert.AreEqual(JellyColor.None, s.GetJelly(1));
            Assert.AreEqual(TileType.Wall, l.GetTile(2));
        }

        [Test]
        public void ParserRejectsBadGrids()
        {
            Assert.Throws<LevelFormatException>(() => TestBoards.State("r.", "r"));
            Assert.Throws<LevelFormatException>(() => TestBoards.State("rx"));
            Assert.Throws<LevelFormatException>(() => LevelData.FromJson("{\"id\":1}"));
            Assert.Throws<LevelFormatException>(() => LevelData.FromJson("{nope"));
        }

        [Test]
        public void ProgressRoundTrips()
        {
            var p = new ProgressData { SoundOn = false, TutorialSeen = true };
            p.RecordWin(1, 4, 20);
            p.RecordWin(2, 7, 20);
            p.RecordWin(2, 5, 20);
            p.RecordWin(2, 9, 20);

            ProgressData back = ProgressData.Deserialize(p.Serialize());
            Assert.AreEqual(3, back.HighestUnlocked);
            Assert.IsFalse(back.SoundOn);
            Assert.IsTrue(back.TutorialSeen);
            Assert.AreEqual(4, back.BestMoves[1]);
            Assert.AreEqual(5, back.BestMoves[2], "best result is kept");
            Assert.IsTrue(back.IsCompleted(2));
            Assert.IsFalse(back.IsCompleted(3));
            Assert.IsTrue(back.IsUnlocked(3));
            Assert.IsFalse(back.IsUnlocked(4));
        }

        [Test]
        public void UnlockStopsAtLastLevel()
        {
            var p = new ProgressData();
            p.HighestUnlocked = 20;
            p.RecordWin(20, 3, 20);
            Assert.AreEqual(20, p.HighestUnlocked);
        }

        [Test]
        public void CorruptProgressFallsBackToDefaults()
        {
            ProgressData p = ProgressData.Deserialize("garbage|u=abc");
            Assert.AreEqual(1, p.HighestUnlocked);
            Assert.IsTrue(p.SoundOn);
            p = ProgressData.Deserialize("v1|u=-4|b=x:y,3:0,4:2");
            Assert.AreEqual(1, p.HighestUnlocked);
            Assert.AreEqual(1, p.BestMoves.Count);
            Assert.AreEqual(2, p.BestMoves[4]);
        }
    }
}
