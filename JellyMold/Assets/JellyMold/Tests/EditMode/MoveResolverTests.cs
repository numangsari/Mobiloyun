using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    public class MoveResolverTests
    {
        [Test]
        public void SingleJellyMovesOneCellIntoMatchingMold()
        {
            BoardState s = TestBoards.State("r.R");
            MoveResult a = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.IsTrue(a.IsValid);
            Assert.AreEqual(new[] { ".rR" }, TestBoards.Grid(a.After));

            MoveResult b = MoveResolver.Apply(a.After, new PressMove(Direction.Right, 0));
            Assert.IsTrue(b.IsValid);
            Assert.AreEqual(1, b.MoldsFilled);
            Assert.AreEqual(new[] { "..*" }, TestBoards.Grid(b.After));
            Assert.IsTrue(b.After.IsSolved);
        }

        [Test]
        public void PressMovesEveryJellyOnTheLineAtMostOneCell()
        {
            BoardState s = TestBoards.State("r.t...");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.AreEqual(new[] { ".r.t.." }, TestBoards.Grid(r.After));
            Assert.AreEqual(2, r.MovedCount);
        }

        [Test]
        public void BackJellyFollowsIntoCellVacatedByFrontJelly()
        {
            BoardState s = TestBoards.State("rt..");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.AreEqual(new[] { ".rt." }, TestBoards.Grid(r.After));
            Assert.AreEqual(0, r.Steps[0].Order);
            Assert.AreEqual(1, r.Steps[0].From, "front jelly (nearest target) resolves first");
        }

        [Test]
        public void JellyBehindBlockedJellyStaysPut()
        {
            BoardState s = TestBoards.State("rt#");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(2, r.Steps.Count);
            Assert.AreEqual(BlockReason.Wall, r.Steps[0].Reason);
            Assert.AreEqual(BlockReason.Jelly, r.Steps[1].Reason);
        }

        [Test]
        public void WrongColourMoldBlocks()
        {
            BoardState s = TestBoards.State("rT.");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(BlockReason.WrongMold, r.Steps[0].Reason);
            Assert.AreSame(s, r.After);
        }

        [Test]
        public void WallBlocks()
        {
            BoardState s = TestBoards.State(".", "#", "t");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Up, 0));
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(BlockReason.Wall, r.Steps[0].Reason);
        }

        [Test]
        public void GridEdgeBlocks()
        {
            BoardState s = TestBoards.State("..y");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(BlockReason.Edge, r.Steps[0].Reason);
        }

        [Test]
        public void FilledMoldBlocksLaterJellies()
        {
            BoardState s = TestBoards.State("rrR");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.AreEqual(new[] { ".r*" }, TestBoards.Grid(r.After), "second jelly follows into the vacated cell");
            MoveResult again = MoveResolver.Apply(r.After, new PressMove(Direction.Right, 0));
            Assert.IsFalse(again.IsValid);
            Assert.AreEqual(BlockReason.FilledMold, again.Steps[0].Reason);
        }

        [Test]
        public void OnePressCanFillSeveralMolds()
        {
            BoardState s = TestBoards.State(
                "rR",
                "tT",
                "yY");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.AreEqual(1, r.MoldsFilled);

            BoardState column = TestBoards.State(
                "r.t",
                "R.T");
            MoveResult c = MoveResolver.Apply(column, new PressMove(Direction.Down, 0));
            Assert.AreEqual(1, c.MoldsFilled);

            BoardState line = TestBoards.State("rRtT");
            MoveResult both = MoveResolver.Apply(line, new PressMove(Direction.Right, 0));
            Assert.AreEqual(2, both.MoldsFilled);
            Assert.IsTrue(both.After.IsSolved);
        }

        [Test]
        public void VerticalPressUsesColumn()
        {
            BoardState s = TestBoards.State(
                "r.",
                "..",
                "R.");
            MoveResult r = MoveResolver.Apply(s, new PressMove(Direction.Down, 0));
            Assert.AreEqual(new[] { "..", "r.", "R." }, TestBoards.Grid(r.After));
            Assert.IsFalse(MoveResolver.Apply(s, new PressMove(Direction.Down, 1)).IsValid);
        }

        [Test]
        public void ApplyDoesNotMutateInput()
        {
            BoardState s = TestBoards.State("r.R");
            string[] before = TestBoards.Grid(s);
            MoveResolver.Apply(s, new PressMove(Direction.Right, 0));
            Assert.AreEqual(before, TestBoards.Grid(s));
        }

        [Test]
        public void OutOfRangeLineIsInvalid()
        {
            BoardState s = TestBoards.State("r.R");
            Assert.IsFalse(MoveResolver.Apply(s, new PressMove(Direction.Right, 3)).IsValid);
            Assert.IsNull(MoveResolver.TryApply(s, new PressMove(Direction.Up, -1)));
        }

        [Test]
        public void SwipeMapsToRowOrColumn()
        {
            Assert.AreEqual(new PressMove(Direction.Left, 4), PressMove.FromSwipe(2, 4, Direction.Left));
            Assert.AreEqual(new PressMove(Direction.Up, 2), PressMove.FromSwipe(2, 4, Direction.Up));
        }
    }
}
