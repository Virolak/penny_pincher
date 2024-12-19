#region imports
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using System.Drawing;
    using QuantConnect;
    using QuantConnect.Algorithm.Framework;
    using QuantConnect.Algorithm.Framework.Selection;
    using QuantConnect.Algorithm.Framework.Alphas;
    using QuantConnect.Algorithm.Framework.Portfolio;
    using QuantConnect.Algorithm.Framework.Execution;
    using QuantConnect.Algorithm.Framework.Risk;
    using QuantConnect.Parameters;
    using QuantConnect.Benchmarks;
    using QuantConnect.Brokerages;
    using QuantConnect.Util;
    using QuantConnect.Interfaces;
    using QuantConnect.Algorithm;
    using QuantConnect.Indicators;
    using QuantConnect.Data;
    using QuantConnect.Data.Consolidators;
    using QuantConnect.Data.Custom;
    using QuantConnect.DataSource;
    using QuantConnect.Data.Fundamental;
    using QuantConnect.Data.Market;
    using QuantConnect.Data.UniverseSelection;
    using QuantConnect.Notifications;
    using QuantConnect.Orders;
    using QuantConnect.Orders.Fees;
    using QuantConnect.Orders.Fills;
    using QuantConnect.Orders.Slippage;
    using QuantConnect.Scheduling;
    using QuantConnect.Securities;
    using QuantConnect.Securities.Equity;
    using QuantConnect.Securities.Future;
    using QuantConnect.Securities.Option;
    using QuantConnect.Securities.Forex;
    using QuantConnect.Securities.Crypto;
    using QuantConnect.Securities.Interfaces;
    using QuantConnect.Storage;
    using QuantConnect.Data.Custom.AlphaStreams;
    using QCAlgorithmFramework = QuantConnect.Algorithm.QCAlgorithm;
    using QCAlgorithmFrameworkBridge = QuantConnect.Algorithm.QCAlgorithm;
#endregion

namespace Crow {

    public class WindowSummary {

        public List<TradeBar> history = new List<TradeBar>();

        // Maximum number of tradebars
        private int m_maxBars = 0;
        public int numBars = 0;
        private TimeSpan m_timespan = TimeSpan.Zero;
        private QCAlgorithm m_algo = null;

        // Has history count reached the maximum number of bars allowed
        public bool isReady = false;

        // Data for most recent tradebar
        public decimal open = 0.0m;
        public decimal close = 0.0m;
        public decimal high = 0.0m;
        public decimal low = 0.0m;
        public decimal volume = 0.0m;
        
        // Sums for calculating averages
        private decimal sumOpen = 0m;
        private decimal sumClose = 0m;
        private decimal sumHigh = 0m;
        private decimal sumLow = 0m;
        private decimal sumVolume = 0m;
        private decimal sumGain = 0m;
        private decimal sumGainPercent = 0m;

        // Averages
        public decimal avgOpen = 0m;
        public decimal avgClose = 0m;
        public decimal avgHigh = 0m;
        public decimal avgLow = 0m;
        public decimal avgVolume = 0m;
        public decimal avgGain = 0m;
        public decimal avgGainPercent = 0m;

        // Totals
        public decimal totalGain = 0m;
        public decimal totalGainPercent = 0m;
        public int liquid = 0;

        public WindowSummary(QCAlgorithm algo, int windowSize) {
            m_maxBars = windowSize;
            m_algo = algo;
        }

        public WindowSummary(QCAlgorithm algo, TimeSpan timespan) {
            m_algo = algo;
            m_timespan = timespan;
        }

        // Updates price summary
        public void update(TradeBar tradeBar) {
            
            // Add tradebar to history
            history.Insert(0, tradeBar);

            // Loop through all tradebars
            while (history.Count > m_maxBars && m_maxBars != 0) {

                // Get oldest bar of data
                TradeBar oldbar = history[history.Count - 1];

                // Remove oldest tradebar
                history.RemoveAt(history.Count - 1);

                // Subtract old bar of data from history
                this.sumOpen -= oldbar.Open;
                this.sumClose -= oldbar.Close;
                this.sumHigh -= oldbar.High;
                this.sumLow -= oldbar.Low;
                this.sumVolume -= oldbar.Volume;
                this.sumGain -= (oldbar.Close - oldbar.Open);
                this.sumGainPercent -= ((oldbar.Close - oldbar.Open) / oldbar.Close) * 100m;
            }

            if (history.Count <= 0) return;

            // Loop through all times
            for (int i = history.Count - 1; i >= 0; i--) {

                if (m_timespan == TimeSpan.Zero) break;

                // Timeframe is up to date
                if (m_algo.Time <= history[i].EndTime.Add(m_timespan)) break;

                // Get oldest bar of data
                TradeBar oldbar = history[i];

                // Remove oldest bar of data
                history.RemoveAt(i);

                // Subtract old bar of data from history
                this.sumOpen -= oldbar.Open;
                this.sumClose -= oldbar.Close;
                this.sumHigh -= oldbar.High;
                this.sumLow -= oldbar.Low;
                this.sumVolume -= oldbar.Volume;
                this.sumGain -= (oldbar.Close - oldbar.Open);
                this.sumGainPercent -= ((oldbar.Close - oldbar.Open) / oldbar.Close) * 100m;

                if (oldbar.Volume > 0) numBars -= 1;

            }

            // Let us know this window is full of data
            if (this.isReady == false && history.Count >= m_maxBars) this.isReady = true;

            // Set window open
            if (history.Count >= 1) this.open = history[history.Count - 1].Open;

            if (tradeBar.Low < this.low || this.low == 0m) this.low = tradeBar.Low;
            if (tradeBar.High > this.high) this.high = tradeBar.High;
            this.close = tradeBar.Close;

            // Update Sums
            this.sumOpen += tradeBar.Open;
            this.sumClose += tradeBar.Close;
            this.sumHigh += tradeBar.High;
            this.sumLow += tradeBar.Low;
            this.sumVolume += tradeBar.Volume;
            this.sumGain += (tradeBar.Close - tradeBar.Open);
            this.sumGainPercent += ((tradeBar.Close - tradeBar.Open) / tradeBar.Open) * 100m;

            // Update averages
            this.avgOpen = this.sumOpen / history.Count;
            this.avgClose = this.sumClose / history.Count;
            this.avgHigh = this.sumHigh / history.Count;
            this.avgLow = this.sumLow / history.Count;
            this.avgVolume = this.sumVolume / history.Count;
            this.avgGain = this.sumGain / history.Count;
            this.avgGainPercent = this.sumGainPercent / history.Count;

            // Update Totals
            this.totalGain = tradeBar.Close - this.history[this.history.Count - 1].Open;
            this.totalGainPercent = (this.totalGain / this.history[this.history.Count - 1].Open) * 100m;

            this.volume = sumVolume;

            if (tradeBar.Volume > 100) numBars += 1;
        }

        // Set all data back to default
        public void reset() {

            // Clear history
            history.Clear();

            // Set all values back to default
            this.isReady = false;
            this.open = this.close = this.high = this.low = this.volume = 0m;
            this.sumOpen = this.sumClose = this.sumHigh = this.sumLow = this.sumVolume = this.sumGain = this.sumGainPercent = 0m;
            this.avgOpen = this.avgClose = this.avgHigh = this.avgLow = this.avgVolume = this.avgGain = this.avgGainPercent = 0m;
            this.totalGain = this.totalGainPercent = 0m;
        }
    }

    public class Asset {

        private QCAlgorithm m_algo;

        // Current symbol
        public Symbol symbol;

        // List of windows
        public List<WindowSummary> windows = new List<WindowSummary>();

        // List of Consolidators
        public List<TradeBarConsolidator> consolidators = new List<TradeBarConsolidator>();

        // List of scheduled events
        public List<ScheduledEvent> scheduledEvents = new List<ScheduledEvent>();

        // Current tradebar data
        public decimal open = 0.0m;
        public decimal close = 0m;
        public decimal high = 0m;
        public decimal low = 0m;
        public decimal volume = 0m;
        public decimal gains = 0m;
        public decimal gainsPercent = 0m;
        public bool isOpen = true;
        public decimal totalVolume = 0m;

        // Initializer
        public Asset(QCAlgorithm algo, Symbol symbol) {

            // Get QCAlgorithm methods
            m_algo = algo;

            // Set the sumbol
            this.symbol = symbol;

            // Add asset to asset manager
            AssetManager.Add(this);
        }

        // Updates based on provided bar of data
        public void update(TradeBar tradebar) {

            // Update current prices
            this.open = tradebar.Open;
            this.close = tradebar.Close;
            this.high = tradebar.High;
            this.low = tradebar.Low;
            this.volume = tradebar.Volume;
            this.gains = tradebar.Close - tradebar.Open;
            this.gainsPercent = (this.gains / tradebar.Open) * 100m;
            this.totalVolume += tradebar.Volume;

            // Update all consolidators
            for(var i = 0; i < consolidators.Count; i++) {
                consolidators[i].Update(tradebar);
            }

        }

        // Adds a rolling window
        public void addWindow(int numBars) {

            // Create a tradebar window summary
            WindowSummary summary = new WindowSummary(m_algo, numBars);

            // Add Window to list of Windows
            windows.Add(summary);
        }

        public void addWindow(TimeSpan timespan) {
            WindowSummary summary = new WindowSummary(m_algo, timespan);
            windows.Add(summary);
        }

        // Adds a tradebar consolidator
        public void addConsolidator(TimeSpan timespan, EventHandler<TradeBar> handler) {
            
            // Create a consolidator
            TradeBarConsolidator consolidator = new TradeBarConsolidator(timespan);
            consolidator.DataConsolidated += handler;

            // Add to list of consolidators
            this.consolidators.Add(consolidator);
        }

        // Adds a scheduled event
        public void addSchedule(ScheduledEvent schedule) {
            scheduledEvents.Add(schedule);
        }

        // Resets all data
        public void reset() {

            // Set values back to default
            this.open = this.close = this.high = this.low = this.volume = this.gains = this.gainsPercent = 0m;

            // Reset data for each window
            for (var i = 0; i < this.windows.Count; i++) {
                this.windows[i].reset();
            }

            this.isOpen = true;
        }

        // Safely handles data linked to asset before deleting
        public void destroy() {

            // Clear all windows
            this.windows.Clear();

            // Clear list of consolidators
            this.consolidators.Clear();

            // Remove scheduled events
            for (int i = 0; i < this.scheduledEvents.Count; i++) {
                m_algo.Schedule.Remove(this.scheduledEvents[i]);
            }

            // Remove from asset manager
            AssetManager.assets.Remove(this.symbol);
        }
    }

    // Manages all assets
    public static class AssetManager {

        // List of Securities
        public static Dictionary<Symbol, Asset> assets = new Dictionary<Symbol, Asset>();
        private static QCAlgorithm m_algo = null;

        // Initializes the asset manager
        public static void init(QCAlgorithm algo) {
            m_algo = algo;
        }

        // Updates Securities in the asset list
        public static void update(TradeBar tradeBar) {

            // Update the asset
            if (assets.ContainsKey(tradeBar.Symbol)) { 
                assets[tradeBar.Symbol].update(tradeBar);
            }

        }

        // Performs action on data
        public static void onData(Slice slice) {

            // Loop through each tradebar in slice and update dictionary
            foreach (TradeBar tradebar in slice.Bars.Values) {
                update(tradebar);
            }

        }

        // Adds an asset to the asset manager
        public static void Add(Asset asset) {

            if (!assets.ContainsKey(asset.symbol)) {
                assets.Add(asset.symbol, asset);
            }
        }

        // Remove a stock from the stock manager
        public static void Remove(Symbol symbol) {

            if (assets.ContainsKey(symbol)) {

                // Remove any data associated with symbol.
                assets[symbol].destroy();
            }
        }

        // Get stock details
        public static Asset Get(Symbol symbol) {

            // Return informaton about symbol
            if (assets.ContainsKey(symbol)) return assets[symbol];
            return null;
        }

        // Resets data for all assets
        public static void reset() {

            // Reset data in window
            foreach (Symbol symbol in assets.Keys) {
                assets[symbol].reset();
            }
        }
    }

    // Analyze a tradebar
    public struct AnalyzedBar {

        public decimal gains = 0m;
        public decimal gainsPercent = 0.0m;
        public decimal open = 0m;
        public decimal close = 0m;
        public decimal high = 0m;
        public decimal low = 0m;
        public decimal volume = 0m;

        AnalyzedBar(TradeBar tradebar) {
            this.open = tradebar.Open;
            this.close = tradebar.Close;
            this.high = tradebar.High;
            this.low = tradebar.Low;
            this.volume = tradebar.Volume;
            this.gains = tradebar.Close - tradebar.Open;
            this.gainsPercent = (this.gains / tradebar.Open) * 100m;
        }

    }
}
