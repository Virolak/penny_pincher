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
    using System.Text.Json;
    using System.Text.Json.Serialization;
#endregion


namespace Crow {

    // Details about holdings in account
    public class HoldingSummary {
        
        private QCAlgorithm m_algo;
        public DateTime timeAdded;

        public HoldingSummary(QCAlgorithm algo) {
            this.m_algo = algo;
            this.timeAdded = m_algo.Time;
        }
    }
    
    // Account Class
    public static class Account {

        // Algorithm
        public static QCAlgorithm m_algo;

        //  Max amount of cash to use per trade
        public static decimal MaxBudget = 10000m;

        // Number of day trades
        public static List<int> day_trades = new List<int>();

        // Max Drawdown percentage before selling holdings at a loss.
        public static decimal maxDrawDownPercent = 5m;

        // Max gains percentage before selling holdings at a profit
        public static decimal maxGainsPercent = 20m;

        // PDT protection enabled
        public static bool pdtProtection = false;

        // Available funds for buying stock
        public static decimal buyingPower = 0m;

        // Percentage of Account Balance to allocate
        public static decimal allocationPercent = 20m;

        // Initialize Account methods. 
        public static void init(QCAlgorithm algo) {
            Account.m_algo = algo;
            Account.buyingPower = m_algo.Portfolio.Cash;
        }

        // Calculates available funds for purchasing assets
        public static decimal getBuyingPower() {

            decimal funds = m_algo.Portfolio.Cash;

            // Loop through all open orders
            foreach(Order order in m_algo.Transactions.GetOpenOrders()) {

                // Only check buy orders
                if (order.Direction != OrderDirection.Buy || order.Type != OrderType.Limit) continue;

                // Get the order ticket
                OrderTicket ticket = m_algo.Transactions.GetOrderTicket(order.Id);

                // Get limit price
                decimal limitPrice = ticket.Get(OrderField.LimitPrice);

                // Subtract from available funds
                funds -= Math.Round(limitPrice * (ticket.Quantity - ticket.QuantityFilled) * 1.05m, 2);
            }

            Account.buyingPower = funds;
            return funds;

        }
    }
}