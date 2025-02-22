namespace Chess
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using static System.Math;
    class MCTSSearch : ISearch
    {
        public event System.Action<Move> onSearchComplete;

        MoveGenerator moveGenerator;

        Move bestMove;
        int bestEval;
        bool abortSearch;

        MCTSSettings settings;
        Board board;
        Evaluation evaluation;

        System.Random rand;

        // Diagnostics
        public SearchDiagnostics Diagnostics { get; set; }
        System.Diagnostics.Stopwatch searchStopwatch;

        public MCTSSearch(Board board, MCTSSettings settings)
        {
            this.board = board;
            this.settings = settings;
            evaluation = new Evaluation();
            moveGenerator = new MoveGenerator();
            rand = new System.Random();
        }

        public void StartSearch()
        {
            InitDebugInfo();
            Debug.Log("Starting MCTS");

            // Initialize search settings
            bestEval = 0;
            bestMove = Move.InvalidMove;

            moveGenerator.promotionsToGenerate = settings.promotionsToSearch;
            abortSearch = false;
            Diagnostics = new SearchDiagnostics();

            SearchMoves();

            onSearchComplete?.Invoke(bestMove);

            if (!settings.useThreading)
            {
                LogDebugInfo();
            }
        }

        public void EndSearch()
        {
            if (settings.useTimeLimit)
            {
                abortSearch = true;
            }
        }

        void SearchMoves()
        {
            var root = new ChessMCTSNode(null, board, moveGenerator, default);

            DateTime startTime = DateTime.Now;
            DateTime endTime =  startTime + TimeSpan.FromMilliseconds(settings.searchTimeMillis*0.9f);


            int iterationCount = 0;
            for(; DateTime.Now < endTime && !abortSearch; )
            {
                //Debug.Log($"Iteration: {iterationCount++}");

                var descendant = root.SelectDescendant(rand);
                for(int tt = 0; tt < 5 && !abortSearch; ++tt)
                {
                    if(root.TryExpand(rand, out var ch))
                    {
                        var outcome = ch.DoSimulate(rand);
                        ch.DoBackpropagate(outcome);
                    }
                }
                for(int tt = 0; tt < 5 && !abortSearch; ++tt)
                {
                    var ch = root.SelectDescendant(rand);
                    var outcome = ch.DoSimulate(rand);
                    ch.DoBackpropagate(outcome);
                }

            }

            // TODO
            // Don't forget to end the search once the abortSearch parameter gets set to true.

            ChessMCTSNode bestChild = root.GetBestChild();
            this.bestMove = bestChild.Move;
            Debug.Log($"Best move: {bestChild.Move.Name} - confidence: {bestChild.Estimate}");
            //throw new NotImplementedException();
        }

        void LogDebugInfo()
        {
            // Optional
        }

        void InitDebugInfo()
        {
            searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Optional
        }
    }
}