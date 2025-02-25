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
            int maxNumOfPlayouts = settings.limitNumOfPlayouts ? settings.maxNumOfPlayouts : (settings.useThreading && settings.useTimeLimit ? int.MaxValue : 1_000_000); //limit the number of playouts to some big but still somewhat reasonable number when there is no time limit

            var root = new ChessMCTSNode(null, board.Clone(), moveGenerator, default);

            for (int iterationsCount = 1; !abortSearch && iterationsCount <= maxNumOfPlayouts; ++iterationsCount)
            {
                var descendant = root.SelectDescendant(int.MaxValue);
                if (!descendant.TryExpand(out var addedNode))
                    addedNode = descendant;
                var result = addedNode.DoSimulate(rand, settings.playoutDepthLimit);
                addedNode.DoBackpropagate(result);

                this.bestMove = root.GetBestChild().Move;
            }
            
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