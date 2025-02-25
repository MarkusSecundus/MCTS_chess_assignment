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

            void log(string message) { }// => Debug.Log("message");

            for (int iterationsCount = 0; !abortSearch && iterationsCount < settings.maxNumOfPlayouts; ++iterationsCount)
            {
                //log("Iteration");
                var descendant = root.SelectDescendant(rand, settings.playoutDepthLimit);
                //log($"Selection performed ({descendant})");
                if (!descendant.TryExpand(rand, out var addedNode))
                {
                    //Debug.LogWarning($"Failed to expand node {descendant}");
                    addedNode = descendant;
                }
                //log($"Expansion performed ({addedNode})");
                var result = addedNode.DoSimulate(rand, 10);
                //log($"Simulation performed ({result})");
                addedNode.DoBackpropagate(result);
                //log("Backpropagation performed");

                this.bestMove = root.GetBestChild().Move;
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