using System.Collections.Generic;

namespace JellyMold.Core
{
    public sealed class SolveResult
    {
        public bool Solvable { get; internal set; }

        /// <summary>Optimal number of presses, -1 when no solution was found.</summary>
        public int Length { get; internal set; } = -1;

        public IReadOnlyList<PressMove> Solution { get; internal set; } = new PressMove[0];
        public int StatesExplored { get; internal set; }

        /// <summary>True when the search stopped at the state or depth limit before exhausting the space.</summary>
        public bool LimitReached { get; internal set; }

        /// <summary>Unsolvable is only proven when the whole reachable space was searched.</summary>
        public bool ProvenUnsolvable => !Solvable && !LimitReached;
    }

    /// <summary>
    /// Breadth-first search over board states. Because every press costs one move,
    /// the first solved state reached is an optimal solution. Visited states are keyed by
    /// <see cref="StateKey"/> so cycles (e.g. left then right) are never re-expanded.
    /// </summary>
    public static class PuzzleSolver
    {
        public static SolveResult Solve(BoardState start, int maxDepth = int.MaxValue, int maxStates = GameConfig.DefaultSolverStateLimit)
        {
            var result = new SolveResult();
            if (start.IsSolved)
            {
                result.Solvable = true;
                result.Length = 0;
                result.StatesExplored = 1;
                return result;
            }

            List<PressMove> moves = PressMove.AllMoves(start.Layout);
            var nodes = new List<Node> { new Node(start, -1, default, 0) };
            var visited = new HashSet<StateKey> { start.Key };

            for (int head = 0; head < nodes.Count; head++)
            {
                Node node = nodes[head];
                if (node.Depth >= maxDepth)
                {
                    result.LimitReached = true;
                    continue;
                }

                foreach (PressMove move in moves)
                {
                    BoardState next = MoveResolver.TryApply(node.State, move);
                    if (next == null || !visited.Add(next.Key)) continue;

                    nodes.Add(new Node(next, head, move, node.Depth + 1));
                    if (next.IsSolved)
                    {
                        result.Solvable = true;
                        result.Length = node.Depth + 1;
                        result.Solution = BuildPath(nodes, nodes.Count - 1);
                        result.StatesExplored = nodes.Count;
                        return result;
                    }

                    if (nodes.Count >= maxStates)
                    {
                        result.LimitReached = true;
                        result.StatesExplored = nodes.Count;
                        return result;
                    }
                }
            }

            result.StatesExplored = nodes.Count;
            return result;
        }

        private static PressMove[] BuildPath(List<Node> nodes, int index)
        {
            var path = new List<PressMove>();
            while (nodes[index].Parent >= 0)
            {
                path.Add(nodes[index].Move);
                index = nodes[index].Parent;
            }
            path.Reverse();
            return path.ToArray();
        }

        private readonly struct Node
        {
            public readonly BoardState State;
            public readonly int Parent;
            public readonly PressMove Move;
            public readonly int Depth;

            public Node(BoardState state, int parent, PressMove move, int depth)
            {
                State = state;
                Parent = parent;
                Move = move;
                Depth = depth;
            }
        }
    }
}
