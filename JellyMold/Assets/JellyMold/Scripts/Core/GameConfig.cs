namespace JellyMold.Core
{
    /// <summary>Central gameplay tuning that both the game and the level tools rely on.</summary>
    public static class GameConfig
    {
        public const int DefaultSolverStateLimit = 400000;

        /// <summary>Extra moves granted on top of the optimal solution length.</summary>
        public static int MoveTolerance(int levelNumber)
        {
            if (levelNumber <= 5) return 3;
            if (levelNumber <= 10) return 2;
            return 1;
        }

        public static int RecommendedMoves(int levelNumber, int optimalLength)
        {
            return optimalLength + MoveTolerance(levelNumber);
        }

        /// <summary>3 stars at par, 2 stars within one extra move, otherwise 1.</summary>
        public static int StarsFor(int movesUsed, int par)
        {
            if (par <= 0) return 1;
            if (movesUsed <= par) return 3;
            if (movesUsed <= par + 1) return 2;
            return 1;
        }
    }
}
