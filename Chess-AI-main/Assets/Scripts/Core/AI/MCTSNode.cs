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
        public int Depth { get; }
        
        double _estimatesSum = 0f;
        int _estimatesCount = 0;
        List<Move> _unexpandedMoves = null;
        List<ChessMCTSNode> _children = null;

        public int ExpandedChildrenCount => _children?.Count ?? 0;

        public bool IsRoot => _parent == null;
        public Move Move { get; }

        const int MaxChildrenCount = int.MaxValue;
        public ChessMCTSNode ParentNode => _parent;

        readonly int _playerIdx;

        public GameCompletionState CompletionState { get; }

        public double Estimate { 
            get
            {
                // if we are the root or just below root
                if (_parent?._parent == null)
                {
                    if (_currentBoard.IsPlayerDefeated(_playerIdx))
                    {
                        return double.NegativeInfinity;
                    }
                    if (_currentBoard.IsPlayerDefeated(1 - _playerIdx))
                    {
                        return double.PositiveInfinity;
                    }
                }

                return _estimatesCount <= 0 ? double.NaN : _estimatesSum / _estimatesCount;
            } 
        }

        public bool CheckIsEndState(out int winnerColor)
        {
            winnerColor = -1;
            if (_parent == null) return false;
            return _parent._currentBoard.IsMoveThatCapturesAKing(this.Move, out winnerColor);
        }

        public ChessMCTSNode GetBestChild() => _children.Max(ch => ch.Estimate);

        public double ComputeUCB()
        {
            // if we are the root or just below root
            if(_parent?._parent == null)
            {
                if (_currentBoard.IsPlayerDefeated(_playerIdx)) return double.NegativeInfinity;
                if (_currentBoard.IsPlayerDefeated(1-_playerIdx)) return double.PositiveInfinity;
            }

            const double C = 1.4142135623730951 //sqrt(2)
                ;
            return (_estimatesSum / _estimatesCount) + C*(Math.Log(_parent?._estimatesCount??0)/_estimatesCount);
        }

        public ChessMCTSNode(ChessMCTSNode parent, Board board, MoveGenerator gen, Move move)
        {
            (_parent, _currentBoard, _moveGenerator, Move) = (parent, board, gen, move);
            Depth = _parent != null ?_parent.Depth + 1 : 0;
            _playerIdx = parent?._playerIdx?? (board.WhiteToMove ? Board.WhiteIndex : Board.BlackIndex);
        }

        public void DoBackpropagate(SimulationResult singleSimulationOutcome)
        {
            _estimatesSum += singleSimulationOutcome.Value;
            _estimatesCount += 1;
            ParentNode?.DoBackpropagate(singleSimulationOutcome);
        }



        public ChessMCTSNode SelectDescendant(System.Random rand, int maxDepth)
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
                    if (ch._unexpandedMoves.Count > 0 && ch.Depth < maxDepth)
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
                _unexpandedMoves = _moveGenerator.GenerateMoves(_currentBoard,this.IsRoot);
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


        public struct SimulationResult
        {
            public double Value { get; }
            public SimulationResult(double val) => Value = val;

            public static SimulationResult Win => new SimulationResult(1);
            public static SimulationResult Loss => new SimulationResult(0);
        }

        public SimulationResult DoSimulate(System.Random rand, int maxMoves = 30)
        {
            var leightweightBoard = _currentBoard.GetLightweightClone();
            var evaluation = new Evaluation();
            bool currentTeam() => _playerIdx == Board.WhiteIndex;
            SimulationResult heuristicEval()=> new SimulationResult(evaluation.EvaluateSimBoard(leightweightBoard, _playerIdx == Board.WhiteIndex));

            while (true)
            {
                if(--maxMoves <= 0)
                {
                    return heuristicEval();
                }
                var availableMoves = _moveGenerator.GetSimMoves(leightweightBoard, currentTeam());
                if(availableMoves.Count <= 0)
                {
                    return heuristicEval();
                }
                for(int t = 0; t < availableMoves.Count; ++t)
                {
                    var move = availableMoves[t];
                    if(leightweightBoard.IsMoveThatCapturesAKing(move, out bool kingTeam))
                    {
                        if (kingTeam != currentTeam())
                            return SimulationResult.Win;
                        else
                        {
                            availableMoves.RemoveAt(t);
                            --t;
                        }
                    }
                }
                if (availableMoves.Count <= 0)
                    return SimulationResult.Loss; // We threw away all moves that would mean our loss and now nothing is left

                var randomMove = availableMoves[rand.Next(availableMoves.Count)];
                leightweightBoard.MakeSimMove(randomMove);
            }
        }



        private string getNamePath()
        {
            if (_parent == null) return "root";
            return $"{_parent.getNamePath()} -> {Move.Name}";
        }
        public override string ToString()
        {
            return $"({_estimatesSum:.0}/{_estimatesCount:.}) {getNamePath()}";
        }
    }










    static class Helpers
    {

        public static bool IsMoveThatCapturesAKing(this Board b, Move m, out int kingColorIdx)
        {
            kingColorIdx = -1;
            var targetSquare = b.Square[m.TargetSquare];
            if(Piece.PieceType(targetSquare) == Piece.King)
            {
                kingColorIdx = Piece.Colour(targetSquare) == Piece.White ? Board.WhiteIndex : Board.BlackIndex;
                return true;
            }
            return false;
        }
        public static bool IsMoveThatCapturesAKing(this SimPiece[,] b, SimMove m, out bool kingColorIdx)
        {
            kingColorIdx = default;
            
            var targetSquare = b[m.endCoord1, m.endCoord2];
            if(targetSquare?.type == SimPieceType.King)
            {
                kingColorIdx = targetSquare.team;
                return true;
            }
            return false;
        }

        public static void MakeSimMove(this SimPiece[,] b, SimMove m)
        {
            b[m.endCoord1, m.endCoord2] = b[m.startCoord2, m.startCoord2];
            b[m.startCoord2, m.startCoord2] = default;
        }

        public static GameCompletionState getBoardState(this Board b, int playerIdx)
        {
            if (b.IsPlayerDefeated(playerIdx)) return GameCompletionState.GameLost;
            if (b.IsPlayerDefeated(1-playerIdx)) return GameCompletionState.GameWon;
            return GameCompletionState.Draw;
        }
        public static bool IsPlayerDefeated(this Board board, int friendlyColourIndex)
        {
            if (Piece.PieceType(board.Square[board.KingSquare[friendlyColourIndex]]) != Piece.King)
            {
                return true;
            }
            return false;
        }

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