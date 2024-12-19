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
    using QuantConnect.Securities.Positions;
    using QuantConnect.Securities.Forex;
    using QuantConnect.Securities.Crypto;
    using QuantConnect.Securities.Interfaces;
    using QuantConnect.Storage;
    using QCAlgorithmFramework = QuantConnect.Algorithm.QCAlgorithm;
    using QCAlgorithmFrameworkBridge = QuantConnect.Algorithm.QCAlgorithm;
#endregion

// Crow namespace
namespace Crow {

    // Penny Stock 
    public class PennyStockFillModel : EquityFillModel {

        private readonly QCAlgorithm m_algo;
        private Dictionary<int, decimal> m_fills = new Dictionary<int, decimal>();

        // Initializer
        public PennyStockFillModel(QCAlgorithm algorithm) {
            m_algo = algorithm;
        }

        // Simulates Limit Order fills
        public override OrderEvent LimitFill(Security security, LimitOrder order) {

            // Get equity fields
            OrderEvent evnt = base.LimitFill(security, order);
            evnt.Status = OrderStatus.None;

            // Get order ticket
            OrderTicket ticket = m_algo.Transactions.GetOrderTicket(order.Id);

            // Keep track of order ticket
            if (!m_fills.ContainsKey(order.Id)) m_fills[order.Id] = ticket.Quantity;

            decimal limitPrice = ticket.Get(OrderField.LimitPrice);
            decimal fill = 0;

            // Fill buy order
                if (order.Direction == OrderDirection.Buy && security.Close <= limitPrice) {
                    fill = Math.Min(m_fills[order.Id], Math.Floor(security.Volume / 10));
                    m_fills[order.Id] -= fill;
                }

                // Fill sell order
                else if (order.Direction == OrderDirection.Sell && security.Close >= limitPrice) {
                    fill = Math.Max(m_fills[order.Id], Math.Floor(security.Volume / 10) * -1);
                    m_fills[order.Id] -= fill;
                }

            evnt.FillQuantity = fill;

            // quantity gets filled
            if (fill != 0) {

                evnt.Status = OrderStatus.PartiallyFilled;
                evnt.FillPrice = limitPrice;

                // Order Filled
                if (m_fills[order.Id] == 0) {
                    evnt.Status = OrderStatus.Filled;
                    m_fills.Remove(order.Id);
                }
            }

            if (order.Status == OrderStatus.Canceled) {
                evnt.Status = OrderStatus.Canceled;
                m_fills.Remove(order.Id);
            }
            
            // Provide fill order
            return evnt;
        }
    }
}
