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
            var root = new ChessMCTSNode(null, board.Clone(), moveGenerator, default);

            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime + TimeSpan.FromMilliseconds(settings.searchTimeMillis*0.9f);


            //while(root.TryExpand(rand, out _)) Debug.Log("expanding root");

            if (true)
            {
                for (int iterationsCount = 0; !abortSearch && iterationsCount < settings.maxNumOfPlayouts; ++iterationsCount)
                {
                    Debug.Log("Iteration");
                    var descendant = root.SelectDescendant(rand);
                    Debug.Log($"Selection performed ({descendant})");
                    if (descendant.TryExpand(rand, out var addedNode))
                    {
                        Debug.Log($"Expansion performed ({addedNode})");
                        var result = addedNode.DoSimulate(rand, settings.playoutDepthLimit);
                        Debug.Log($"Simulation performed ({result})");
                        addedNode.DoBackpropagate(result);
                        Debug.Log("Backpropagation performed");
                    }
                    else
                    {
                        Debug.LogError($"Failed to expand node {descendant}");
                        break;
                    }
                }
            }


            // TODO
            // Don't forget to end the search once the abortSearch parameter gets set to true.

            ChessMCTSNode bestChild = root.GetBestChild();
            this.bestMove = bestChild.Move;
            Debug.Log($"Best move: {bestChild.Move.Name} - confidence: {bestChild.Estimate}");
            
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