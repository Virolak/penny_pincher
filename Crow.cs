#region imports
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Globalization;
    using System.Drawing;
    using QuantConnect;
    using QuantConnect.Algorithm.CSharp;
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

    // Crow class
    public static class Crow {

        private static QCAlgorithm m_algo = null;

        // Initializes the crow algorithm
        public static void init(QCAlgorithm algorithm) {

            m_algo = algorithm;

            // Initialize the asset manager
            AssetManager.init(algorithm);
            Account.init(algorithm);
        }

        // Places a market order on specified symbol
        public static OrderTicket buy(Symbol symbol, decimal quantity) {
           return m_algo.MarketOrder(symbol, Math.Abs(quantity));
        }

        // Places Limit order on specified symbol
        public static OrderTicket buy(Symbol symbol, decimal quantity, decimal price) {

            // Subtract from buying power
            Account.buyingPower -= Math.Abs(quantity) * price;

            return m_algo.LimitOrder(symbol, Math.Abs(quantity), price);
        }

        // Places a market sell order
        public static OrderTicket sell(Symbol symbol, decimal quantity) {

            // Order ticket
            OrderTicket ticket = null;

            // Ensure there are enough shares in portfolio to sell
            if (!m_algo.Portfolio[symbol].Invested) return ticket;

            // Place the order
            ticket = m_algo.MarketOrder(symbol, Math.Abs(quantity) * -1);

            // Return the order ticket
            return ticket;
        }

        // Places a limit sell order
        public static OrderTicket sell(Symbol symbol, decimal quantity, decimal price) {

            // Order ticket
            OrderTicket ticket = null;

            // Ensure there are enough shares in Portfolio to sell
            if (!m_algo.Portfolio[symbol].Invested) return ticket;

            // Sell the order
            ticket = m_algo.LimitOrder(symbol, Math.Abs(quantity) * -1, price);

            // Return the order ticket
            return ticket;
        }

        // On order Event
        public static void updateOrderEvents(OrderEvent evnt) {

            if (m_algo == null) return;

            // Get order ticket
            OrderTicket ticket = m_algo.Transactions.GetOrderTicket(evnt.OrderId);
            Order order = m_algo.Transactions.GetOrderById(evnt.OrderId);

            // Check if this is a buy order
            if (order.Direction == OrderDirection.Buy) {

                // check if this is a limit order
                if (order.Type == OrderType.Limit) {
                    decimal limitPrice = ticket.Get(OrderField.LimitPrice);

                    // Add buying power back from ticket if ticket is cancelled or invalid
                    if (order.Status == OrderStatus.Canceled || order.Status == OrderStatus.Invalid) {
                        Account.buyingPower += (ticket.Quantity - ticket.QuantityFilled) * limitPrice;
                    }
                }

                // Check if order is a market order
                if (order.Type == OrderType.Market) {
                    if (evnt.FillQuantity != 0) Account.buyingPower -= evnt.FillQuantity * evnt.FillPrice;
                }
            }

            // Check if this is a sell order
            if (order.Direction == OrderDirection.Sell) {

                // Add back to buying power
                if (evnt.FillQuantity != 0) {
                    if (m_algo.BrokerageModel.AccountType == AccountType.Margin) Account.buyingPower += Math.Abs(evnt.FillQuantity) * evnt.FillPrice;
                };
            
            }
        }
    }
}
