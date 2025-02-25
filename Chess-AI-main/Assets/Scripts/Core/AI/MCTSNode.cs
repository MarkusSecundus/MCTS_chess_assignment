namespace Chess
{
    using Chess.Game;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.VisualScripting;
    using UnityEngine;
    using UnityEngine.Assertions;


    public class ChessMCTSNode
    {
        Board _thisBoard;
        MoveGenerator _moveGenerator;

        public ChessMCTSNode Parent { get; }
        public bool IsRoot => Parent == null;
        public int Depth { get; }
        
        public float EstimatesSum { get; private set; } = 0f;
        public float EstimatesCount { get; private set; } = 0;

        readonly List<Move> _unexpandedMoves;
        readonly List<ChessMCTSNode> _children = new();

        public int TotalChildrenCount => (_children?.Count ?? 0) + (_unexpandedMoves?.Count??0);

        public Move Move { get; }

        readonly int _thisPlayerIdx;

        public float Estimate => EstimatesCount <= 0 ? 0f : EstimatesSum / EstimatesCount;

        public bool CheckIsEndState(out int loserColor)
        {
            loserColor = -1;
            if (Parent == null) return false;
            return Parent._thisBoard.IsMoveThatCapturesAKing(this.Move, out loserColor);
        }

        public ChessMCTSNode GetBestChild() => _children.MaxOrDefault(ch => ch.Estimate);

        public float ComputeUCB()
        {
            const float C = 1f;
            return (EstimatesSum / EstimatesCount) + C*(Mathf.Log(Parent?.EstimatesCount??0)/EstimatesCount);
        }

        public ChessMCTSNode(ChessMCTSNode parent, Board board, MoveGenerator gen, Move move)
        {
            (Parent, _thisBoard, _moveGenerator, Move) = (parent, board, gen, move);
            Depth = (Parent == null) ? 0: (Parent.Depth + 1);
            _thisPlayerIdx = (parent == null) ? (board.WhiteToMove ? Board.WhiteIndex : Board.BlackIndex) : parent._thisPlayerIdx;

            _unexpandedMoves = _moveGenerator.GenerateMoves(_thisBoard, this.IsRoot);
        }

        public void DoBackpropagate(SimulationResult singleSimulationOutcome, float decayRate = 1.0f)
        {
            float weight = 1.0f;
            for(ChessMCTSNode node = this; node != null; node = node.Parent)
            {
                node.EstimatesSum += singleSimulationOutcome.Value * weight;
                node.EstimatesCount += weight;
                weight *= decayRate;
            }
        }



        public ChessMCTSNode SelectDescendant(int maxDepth)
        {
            ChessMCTSNode ret = this;
            while (true)
            {
                if (ret._unexpandedMoves.Count > 0)
                    return ret;

                if (ret.Depth >= maxDepth)
                    return ret;

                var child = ret._children.MaxOrDefault(ch => ch.ComputeUCB());
                if (child == null) return ret;
                ret = child;
            }
        }


        public bool TryExpand(out ChessMCTSNode addedNode)
        {
            addedNode = null;


            if (_unexpandedMoves.Count <= 0)
                return false; // there is no move left to expand new child


            var moveToExpand = _unexpandedMoves[_unexpandedMoves.Count - 1];
            _unexpandedMoves.RemoveAt(_unexpandedMoves.Count - 1);

            var expandedBoard = _thisBoard.Clone(); 
            expandedBoard.MakeMove(moveToExpand);

            _children.Add(addedNode = new ChessMCTSNode(this, expandedBoard, _moveGenerator, moveToExpand));
            return true;
        }


        public struct SimulationResult
        {
            public float Value { get; }
            public SimulationResult(float val) => Value = val;

            public static SimulationResult Win => new SimulationResult(1f);
            public static SimulationResult Loss => new SimulationResult(0f);

            public override string ToString() => Value.ToString();
        }

        public SimulationResult DoSimulate(System.Random rand, int maxMoves)
        {
            if(this.CheckIsEndState(out int loserColor))
            {
                return (loserColor == _thisPlayerIdx) ? SimulationResult.Loss : SimulationResult.Win;
            }

            var board = _thisBoard.GetLightweightClone();
            bool playerOnTurn = _thisBoard.WhiteToMove;
            var evaluation = new Evaluation();
            bool thisPlayer() => _thisPlayerIdx == Board.WhiteIndex;
            SimulationResult heuristicEval() => new SimulationResult(evaluation.EvaluateSimBoard(board, thisPlayer()));

            while (true)
            {
                if(--maxMoves <= 0)
                    return heuristicEval();

                var availableMoves = _moveGenerator.GetSimMoves(board, playerOnTurn);
                if(availableMoves.Count <= 0)
                    return heuristicEval();
                                
                var randomMove = availableMoves[rand.Next(availableMoves.Count)];
                if (board.IsMoveThatCapturesAKing(randomMove, out bool kingTeam))
                    return (kingTeam != thisPlayer()) ? SimulationResult.Win : SimulationResult.Loss;

                board.MakeSimMove(randomMove);
                playerOnTurn = !playerOnTurn;
            }
        }



        string _getNamePath()
        {
            if (Parent == null) return "root";
            return $"{Parent._getNamePath()} -> {Move.Name}";
        }
        public override string ToString()
        {
            return $"({EstimatesSum:.0}/{EstimatesCount:.}) {_getNamePath()}";
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
            b[m.endCoord1, m.endCoord2] = b[m.startCoord1, m.startCoord2];
            b[m.startCoord1, m.startCoord2] = default;
        }

        public static bool IsPlayerDefeated(this Board board, int friendlyColourIndex)
        {
            if (Piece.PieceType(board.Square[board.KingSquare[friendlyColourIndex]]) != Piece.King) 
            {
                return true;
            }
            return false;
        }

        public static TElem MaxOrDefault<TElem, TComp>(this IEnumerable<TElem> self, Func<TElem, TComp> selector) where TComp: IComparable<TComp>
        {
            using var it = self.GetEnumerator();
            if (!it.MoveNext()) return default;
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