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

    // A watchlist
    public class Watchlist {

        private QCAlgorithm m_algo = null;

        // Class Details
        public class Info {
            public Symbol symbol;
            public DateTime timeAdded;
            public DateTime lastUpdate;
            public string description;

            // Constructor
            public Info(Symbol symbol, QCAlgorithm algo) {
                this.symbol = symbol; ///< Asset symbol
                this.timeAdded = algo.Time; ///< Time added
                this.lastUpdate = algo.Time; ///< Last time updated
            }
        }

        // Dictionary containing keys and details
        public Dictionary<Symbol, Info> symbols = new Dictionary<Symbol, Info>();

        // Class constructor
        public Watchlist(QCAlgorithm algo) {
            this.m_algo = algo;
        }

        // adds a symbol to the watchlist
        public void add(Symbol symbol, string description="") {

            // Update the last update filed if key is already in list
            if (symbols.ContainsKey(symbol)) {
                symbols[symbol].lastUpdate = m_algo.Time;
                return;
            }

            // Add symbol to watchlist
            this.symbols.Add(symbol, new Info(symbol, this.m_algo));
        }

        // Remove symbol from watchlist
        public void remove(Symbol symbol) {

            // Exit if symbol does not exist in watchlist
            if (!symbols.ContainsKey(symbol)) return;

            symbols.Remove(symbol);
        }

        // Get symbol informatoin from watchlist
        public Info get(Symbol symbol) {

            if (symbols.ContainsKey(symbol)) return symbols[symbol];
            return null;
        }
    }

}
