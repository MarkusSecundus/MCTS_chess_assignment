namespace Chess
{
    using Chess.Game;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.VisualScripting;
    using UnityEngine;
    using UnityEngine.Assertions;

    public enum GameCompletionState
    {
        InProgress,
        GameWon,
        Draw,
        GameLost
    }


    public class ChessMCTSNode
    {
        Board _currentBoard;
        MoveGenerator _moveGenerator;

        ChessMCTSNode _parent;
        double _estimatesSum = 0f;
        int _estimatesCount = 0;
        List<Move> _unexpandedMoves = null;
        List<ChessMCTSNode> _children = null;

        public int ExpandedChildrenCount => _children?.Count ?? 0;

        public Move Move { get; }

        const int MaxChildrenCount = int.MaxValue;
        public ChessMCTSNode ParentNode => _parent;

        readonly int _playerIdx;

        public GameCompletionState CompletionState { get; }

        public double Estimate => _estimatesCount <= 0 ? double.NaN : _estimatesSum / _estimatesCount;

        public ChessMCTSNode GetBestChild() => _children.Max(ch => ch.Estimate);

        public double ComputeUCB()
        {
            const double C = 1.4142135623730951 //sqrt(2)
                ;
            return (_estimatesSum / _estimatesCount) + C*(Math.Log(_parent?._estimatesCount??0)/_estimatesCount);
        }

        public ChessMCTSNode(ChessMCTSNode parent, Board board, MoveGenerator gen, Move move)
        {
            (_parent, _currentBoard, _moveGenerator, Move) = (parent, board, gen, move);
            _playerIdx = parent?._playerIdx?? (board.WhiteToMove ? Board.WhiteIndex : Board.BlackIndex);
        }

        public void DoBackpropagate(GameCompletionState singleSimulationOutcome)
        {
            _estimatesSum += singleSimulationOutcome switch
            {
                GameCompletionState.GameWon => 1f,
                GameCompletionState.Draw => 0.5f,
                GameCompletionState.InProgress => 0.5f,
                GameCompletionState.GameLost => 0f,
                _ => throw new ArgumentException(),
            };
            _estimatesCount += 1;
            ParentNode?.DoBackpropagate(singleSimulationOutcome);
        }



        public ChessMCTSNode SelectDescendant(System.Random rand)
        {
            _makeSureMovesAreGenerated();
            if (_unexpandedMoves.Count > 0)
                return this;//throw new InvalidOperationException($"No descendant to select - consider calling {nameof(TryExpand)} first!");

            // recursively expand child nodes with biggest UCB until we get to a node that hadn't yet been simulated
            ChessMCTSNode ret = this;
            while (ret._estimatesCount > 0 && ret.ExpandedChildrenCount > 0)
            {
                // since this node was already simulated at least once, we expect that the children array must have been initialized
                ChessMCTSNode maxChild = null;
                double maxUCB = double.NegativeInfinity;
                foreach(var ch in ret._children)
                {
                    ch._makeSureMovesAreGenerated();
                    if (ch.ExpandedChildrenCount == 0 && ch._unexpandedMoves.Count == 0)
                    {
                        continue;
                    }
                    if (ch._unexpandedMoves.Count > 0)
                    {
                        maxChild = ch;
                        break;
                    }
                    double ucb = ch.ComputeUCB();
                    if (ucb > maxUCB)
                        (maxUCB, maxChild) = (ucb, ch);
                }
                if (maxChild == null) break;
                ret = maxChild;
            }
            return ret;
        }

        void _makeSureMovesAreGenerated()
        {
            if (_unexpandedMoves == null)
            {
                Assert.IsNull(_children);
                _unexpandedMoves = _moveGenerator.GenerateMoves(_currentBoard, true, true);
                _children = new();
            }
            Assert.IsNotNull(_children);
        }

        public bool TryExpand(System.Random rand, out ChessMCTSNode addedNode)
        {
            _ = rand; // for the generic interface it makes very good sense to have Random generator provided, but here, we use deterministic (last move -> first child) policy
            addedNode = null;

            Debug.Log("Starting expansion");
            _makeSureMovesAreGenerated();
            if (_unexpandedMoves.Count <= 0)
                return false; // there is no move left to expand new child

            Debug.Log($"Moves are generated ({_children.Count} expanded | {_unexpandedMoves.Count} unexpanded)");
            var moveToExpand = _unexpandedMoves[^1];
            _unexpandedMoves.RemoveAt(_unexpandedMoves.Count - 1);
            Debug.Log($"Getting a move to expand: {moveToExpand.Name}");

            var expandedBoard = _currentBoard.Clone(); 
            expandedBoard.MakeMove(moveToExpand);

            _children.Add(addedNode = new ChessMCTSNode(this, expandedBoard, _moveGenerator, moveToExpand));
            return true;
        }

        public GameCompletionState DoSimulate(System.Random rand, int maxMoves = 30)
        {
            var b = _currentBoard.Clone();
            while( ! (b.IsPlayerDefeated(Board.WhiteIndex) || b.IsPlayerDefeated(Board.BlackIndex))) // if one of the kings was taken, that means game over
            {
                if(--maxMoves <= 0)
                {
                    return GameCompletionState.Draw;
                }

                var availableMoves = _moveGenerator.GenerateMoves(b, false, false);
                if(availableMoves.Count <= 0)
                {
                    return b.IsPlayerDefeated(_playerIdx) ? GameCompletionState.GameLost : GameCompletionState.GameWon;
                }
                var randomMove = availableMoves[rand.Next(availableMoves.Count)];
                b.MakeMove(randomMove);
            }
            return b.IsPlayerDefeated(_playerIdx) ? GameCompletionState.GameLost : GameCompletionState.GameWon;
        }


        public override string ToString()
        {
            if (this._parent == null) return "root";
            return $"{_parent.ToString()} -> {Move.Name}";
        }
    }










    static class Helpers
    {

        public static bool IsPlayerDefeated(this Board board, int friendlyColourIndex) => Piece.PieceType(board.Square[board.KingSquare[friendlyColourIndex]]) != Piece.King;

        public static TElem Max<TElem, TComp>(this IEnumerable<TElem> self, Func<TElem, TComp> selector) where TComp: IComparable<TComp>
        {
            using var it = self.GetEnumerator();
            if (!it.MoveNext()) throw new ArgumentOutOfRangeException();
            TComp maxValue = selector(it.Current);
            TElem ret = it.Current;
            while (it.MoveNext())
            {
                var value = selector(it.Current);
                if (value.CompareTo(maxValue) > 0)
                    (ret, maxValue) = (it.Current, value);
            }
            return ret;
        }
    }
}