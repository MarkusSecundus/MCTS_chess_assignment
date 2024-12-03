namespace Chess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;


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

        public IEnumerable<TDerived> ExpandRandomChildren(int maxChildrenCount, System.Random rand);

        public GameCompletionState Simulate(System.Random rand);

        public void DoBackpropagate(GameCompletionState singleSimulationOutcome, float weight = 1.0f);
    }

    public class ChessMCTSNode : IMCTSNode<ChessMCTSNode>
    {
        Board _currentBoard;
        MoveGenerator _moveGenerator;

        ChessMCTSNode _parent;
        double _estimatesSum = 0f;
        int _estimatesCount = 0;
        public ChessMCTSNode ParentNode => _parent;

        public GameCompletionState CompletionState { get; }

        public double GetEstimate => _estimatesCount <= 0 ? double.NaN : _estimatesSum / _estimatesCount;

        public ChessMCTSNode(ChessMCTSNode parent, Board board, MoveGenerator gen)
        {
            (_parent, _currentBoard, _moveGenerator) = (parent, board, gen);
        }

        public void DoBackpropagate(GameCompletionState singleSimulationOutcome, float weight = 1.0f)
        {
            _estimatesSum += singleSimulationOutcome switch
            {
                GameCompletionState.GameWon => 1f,
                GameCompletionState.Draw => 0.5f,
                GameCompletionState.GameLost => 0f,
                _ => throw new ArgumentException(),
            };
            ++_estimatesCount;
            ParentNode?.DoBackpropagate(singleSimulationOutcome, weight * 0.75f);
        }

        public IEnumerable<ChessMCTSNode> ExpandRandomChildren(int maxChildrenCount, System.Random rand)
        {
            var moves = _moveGenerator.GenerateMoves(_currentBoard, true);
            while (moves.Count > maxChildrenCount) moves.RemoveAt(rand.Next(moves.Count));
            foreach(var move in moves)
            {
                var ret = _currentBoard.Clone();
                ret.MakeMove(move);
                yield return new ChessMCTSNode(this, ret, _moveGenerator);
            }
        }

        public GameCompletionState Simulate(System.Random rand)
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
}