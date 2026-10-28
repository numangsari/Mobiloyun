using JellyMold.Core;

namespace JellyMold.Tests
{
    /// <summary>Builds boards from the same grid notation the level files use.</summary>
    internal static class TestBoards
    {
        public static BoardState State(params string[] rows)
        {
            var level = new LevelData { Id = 99, Grid = rows };
            return level.CreateInitialState();
        }

        public static string[] Grid(BoardState state) => LevelData.ToGrid(state);

        public static GameSession Session(int moves, params string[] rows)
        {
            return new GameSession(State(rows), moves);
        }
    }
}
