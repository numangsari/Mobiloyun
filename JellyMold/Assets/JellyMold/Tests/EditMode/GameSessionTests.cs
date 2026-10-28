using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    public class GameSessionTests
    {
        private static readonly PressMove Right0 = new PressMove(Direction.Right, 0);
        private static readonly PressMove Left0 = new PressMove(Direction.Left, 0);

        [Test]
        public void InvalidMoveDoesNotConsumeMoves()
        {
            GameSession g = TestBoards.Session(3, "r.R");
            MoveResult r = g.TryPress(Left0);
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(3, g.MovesLeft);
            Assert.IsFalse(g.CanUndo);
        }

        [Test]
        public void WinWhenAllMoldsFilled()
        {
            GameSession g = TestBoards.Session(3, "r.R");
            g.TryPress(Right0);
            Assert.AreEqual(GameStatus.Playing, g.Status);
            g.TryPress(Right0);
            Assert.AreEqual(GameStatus.Won, g.Status);
            Assert.AreEqual(2, g.MovesUsed);
        }

        [Test]
        public void WinningOnTheLastMoveIsAWin()
        {
            GameSession g = TestBoards.Session(2, "r.R");
            g.TryPress(Right0);
            g.TryPress(Right0);
            Assert.AreEqual(GameStatus.Won, g.Status);
        }

        [Test]
        public void LoseWhenMovesRunOutWithEmptyMolds()
        {
            GameSession g = TestBoards.Session(1, "r.R");
            g.TryPress(Right0);
            Assert.AreEqual(GameStatus.Lost, g.Status);
            Assert.IsFalse(g.TryPress(Right0).IsValid, "no moves accepted after losing");
        }

        [Test]
        public void UndoRestoresPositionsMoldsAndMoveCount()
        {
            GameSession g = TestBoards.Session(5, "rR.", "t.T");
            BoardState start = g.Current;
            g.TryPress(Right0);
            BoardState afterFill = g.Current;
            Assert.AreEqual(1, afterFill.FilledCount);
            g.TryPress(new PressMove(Direction.Right, 1));

            Assert.IsTrue(g.Undo());
            Assert.IsTrue(g.Current.SameAs(afterFill));
            Assert.AreEqual(4, g.MovesLeft);

            Assert.IsTrue(g.Undo());
            Assert.IsTrue(g.Current.SameAs(start));
            Assert.AreEqual(0, g.Current.FilledCount);
            Assert.AreEqual(5, g.MovesLeft);
            Assert.IsFalse(g.Undo());
        }

        [Test]
        public void UndoAfterLosingResumesPlay()
        {
            GameSession g = TestBoards.Session(1, "r.R");
            g.TryPress(Right0);
            Assert.AreEqual(GameStatus.Lost, g.Status);
            g.Undo();
            Assert.AreEqual(GameStatus.Playing, g.Status);
            Assert.AreEqual(1, g.MovesLeft);
        }

        [Test]
        public void RestartReturnsToInitialState()
        {
            GameSession g = TestBoards.Session(4, "r.R");
            g.TryPress(Right0);
            g.TryPress(Right0);
            g.Restart();
            Assert.AreSame(g.Initial, g.Current);
            Assert.AreEqual(4, g.MovesLeft);
            Assert.AreEqual(GameStatus.Playing, g.Status);
            Assert.IsFalse(g.CanUndo);
        }

        [Test]
        public void InputGateRejectsInputWhileBusy()
        {
            var gate = new InputGate();
            Assert.IsTrue(gate.TryAcquire());
            Assert.IsFalse(gate.TryAcquire(), "second rapid press is ignored while animating");
            gate.Release();
            Assert.IsTrue(gate.TryAcquire());
        }

        [Test]
        public void RapidRepeatedPressesAreEachResolvedDeterministically()
        {
            GameSession a = TestBoards.Session(10, "r....R");
            GameSession b = TestBoards.Session(10, "r....R");
            for (int i = 0; i < 8; i++) a.TryPress(Right0);
            for (int i = 0; i < 5; i++) b.TryPress(Right0);
            Assert.AreEqual(GameStatus.Won, a.Status);
            Assert.AreEqual(5, a.MovesUsed, "presses after winning are ignored");
            Assert.AreEqual(TestBoards.Grid(b.Current), TestBoards.Grid(a.Current));
        }

        [Test]
        public void HasAnyValidMoveDetectsStuckBoards()
        {
            Assert.IsTrue(TestBoards.Session(3, "r.R").HasAnyValidMove());
            Assert.IsFalse(TestBoards.Session(3, "rT").HasAnyValidMove());
        }

        [Test]
        public void StarsFollowPar()
        {
            Assert.AreEqual(3, GameConfig.StarsFor(4, 4));
            Assert.AreEqual(2, GameConfig.StarsFor(5, 4));
            Assert.AreEqual(1, GameConfig.StarsFor(7, 4));
        }
    }
}
