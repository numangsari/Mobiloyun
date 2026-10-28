using System.Collections.Generic;

namespace JellyMold.Core
{
    /// <summary>
    /// Deterministic press rules.
    /// Jellies on the pressed line are resolved from the one nearest the press target backwards;
    /// each advances at most one cell, so a jelly may follow into the cell its front neighbour just left.
    /// A jelly entering an empty mold of its own colour fills it and leaves the board.
    /// </summary>
    public static class MoveResolver
    {
        public static MoveResult Apply(BoardState state, PressMove move)
        {
            var steps = new List<JellyStep>();
            BoardState next = Resolve(state, move, steps);
            return new MoveResult(move, state, next ?? state, steps, true);
        }

        /// <summary>Allocation-light variant for the solver. Returns null when nothing moves.</summary>
        public static BoardState TryApply(BoardState state, PressMove move)
        {
            return Resolve(state, move, null);
        }

        private static BoardState Resolve(BoardState state, PressMove move, List<JellyStep> steps)
        {
            BoardLayout layout = state.Layout;
            Direction dir = move.Direction;
            if (move.Line < 0 || move.Line >= layout.LineCount(dir)) return null;

            JellyColor[] jellies = null;
            bool[] filled = null;
            int dx = dir.Dx(), dy = dir.Dy();
            int length = layout.LineLength(dir);
            int order = 0;
            bool anyMoved = false;

            for (int k = 0; k < length; k++)
            {
                // k = 0 is the cell nearest the press target.
                int along = (dir == Direction.Right || dir == Direction.Down) ? length - 1 - k : k;
                int x = dir.IsHorizontal() ? along : move.Line;
                int y = dir.IsHorizontal() ? move.Line : along;
                int cell = layout.Index(x, y);

                JellyColor color = jellies != null ? jellies[cell] : state.GetJelly(cell);
                if (color == JellyColor.None) continue;

                int tx = x + dx, ty = y + dy;
                BlockReason reason = BlockReason.None;
                int target = -1;
                bool entersMold = false;

                if (!layout.InBounds(tx, ty))
                {
                    reason = BlockReason.Edge;
                }
                else
                {
                    target = layout.Index(tx, ty);
                    switch (layout.GetTile(target))
                    {
                        case TileType.Wall:
                            reason = BlockReason.Wall;
                            break;
                        case TileType.Mold:
                            int mold = layout.GetMoldIndex(target);
                            bool isFilled = filled != null ? filled[mold] : state.IsMoldFilled(mold);
                            if (isFilled) reason = BlockReason.FilledMold;
                            else if (layout.GetMoldColor(target) != color) reason = BlockReason.WrongMold;
                            else entersMold = true;
                            break;
                        default:
                            JellyColor occupant = jellies != null ? jellies[target] : state.GetJelly(target);
                            if (occupant != JellyColor.None) reason = BlockReason.Jelly;
                            break;
                    }
                }

                if (reason != BlockReason.None)
                {
                    steps?.Add(new JellyStep(cell, cell, color, StepKind.Blocked, reason, order++));
                    continue;
                }

                if (jellies == null)
                {
                    jellies = state.CopyJellies();
                    filled = state.CopyFilled();
                }

                jellies[cell] = JellyColor.None;
                if (entersMold) filled[layout.GetMoldIndex(target)] = true;
                else jellies[target] = color;
                anyMoved = true;
                steps?.Add(new JellyStep(cell, target, color, entersMold ? StepKind.EnteredMold : StepKind.Moved, BlockReason.None, order++));
            }

            return anyMoved ? new BoardState(layout, jellies, filled) : null;
        }
    }
}
