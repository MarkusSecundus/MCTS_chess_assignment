namespace Chess
{
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

    public interface IMCTSNode<TDerived> where TDerived : IMCTSNode<TDerived>
    {
        public TDerived ParentNode { get; }
        public GameCompletionState CompletionState { get; } // info about whether the game is over and who won

        public double GetEstimate { get; }

        public TDerived SelectDescendant(System.Random rand);

        public GameCompletionState DoSimulate(System.Random rand);
        public bool TryExpand(System.Random rand, out TDerived addedNode);

        public void DoBackpropagate(GameCompletionState singleSimulationOutcome);
    }

    public class ChessMCTSNode : IMCTSNode<ChessMCTSNode>
    {
        Board _currentBoard;
        MoveGenerator _moveGenerator;

        ChessMCTSNode _parent;
        double _estimatesSum = 0f;
        int _estimatesCount = 0;
        List<Move> _unexpandedMoves = null;
        List<ChessMCTSNode> _children = null;

        const int MaxChildrenCount = int.MaxValue;
        public ChessMCTSNode ParentNode => _parent;

        public GameCompletionState CompletionState { get; }

        public double GetEstimate => _estimatesCount <= 0 ? double.NaN : _estimatesSum / _estimatesCount;

        public double ComputeUCB()
        {
            const double C = 1//.4142135623730951 //sqrt(2)
                ;
            return (_estimatesSum / _estimatesCount) + C*(Math.Log(_parent._estimatesCount)/_estimatesCount);
        }

        public ChessMCTSNode(ChessMCTSNode parent, Board board, MoveGenerator gen)
        {
            (_parent, _currentBoard, _moveGenerator) = (parent, board, gen);
        }

        public void DoBackpropagate(GameCompletionState singleSimulationOutcome)
        {
            _estimatesSum += singleSimulationOutcome switch
            {
                GameCompletionState.GameWon => 1f,
                GameCompletionState.Draw => 0.5f,
                GameCompletionState.GameLost => 0f,
                _ => throw new ArgumentException(),
            };
            _estimatesCount += 1;
            ParentNode?.DoBackpropagate(singleSimulationOutcome);
        }



        public ChessMCTSNode SelectDescendant(System.Random rand)
        {
            if (_children == null || _children.Count <= 0)
                throw new InvalidOperationException($"No descendant to select - consider calling {nameof(TryExpand)} first!");

            // recursively expand child nodes with biggest UCB until we get to a node that hadn't yet been simulated
            ChessMCTSNode ret = this;
            while (ret._estimatesCount > 0)
            {
                // since this node was already simulated at least once, we expect that the children array must have been initialized
                ret = ret._children.Max(ch => ch.ComputeUCB());
            }
            return ret;
        }
        public bool TryExpand(System.Random rand, out ChessMCTSNode addedNode)
        {
            _ = rand; // for the generic interface it makes very good sense to have Random generator provided, but here, we use deterministic (last move -> first child) policy
            addedNode = default;

            if(_unexpandedMoves == null)
            {
                _unexpandedMoves = _moveGenerator.GenerateMoves(_currentBoard, true);
                Assert.IsNull(_children);
                _children = new();
            }
            Assert.IsNotNull(_children);
            if (_unexpandedMoves.Count <= 0)
                return false; // there is no move left to expand new child

            var moveToExpand = _unexpandedMoves[^1];
            _unexpandedMoves.RemoveAt(_unexpandedMoves.Count - 1);
            
            var expandedBoard = _currentBoard.Clone();
            expandedBoard.MakeMove(moveToExpand);

            _children.Add(addedNode = new ChessMCTSNode(this, expandedBoard, _moveGenerator));
            return true;
        }

        public GameCompletionState DoSimulate(System.Random rand)
        {
            var b = _currentBoard.Clone();
            while(b.KingSquare.Length >= 2 && b.KingSquare.All(k=>k>0)) // if one of the kings was taken, that means game over
            {
                var availableMoves = _moveGenerator.GenerateMoves(_currentBoard, true);
                var randomMove = availableMoves[rand.Next(availableMoves.Count)];
                b.MakeMove(randomMove);
            }
            return default; // I have no idea how we are supposed to tell from a `Board` instance which side did win
        }
    }


    static class Helpers
    {
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