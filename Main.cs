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

namespace QuantConnect.Algorithm.CSharp {
    
    // MinuteMen algorithm
    public class PennyPincher : QCAlgorithm {

        // Global Variables
        decimal MAX_FUNDS_PER_STOCK = 0m;
        decimal MIN_FUNDS_PER_STOCK = 0m;
        decimal RESERVED_FUNDS = 0m;
        decimal BUYING_POWER = 0m;
        decimal MONEY_CAP = 200;

        // Amount of money to trade with per day
        decimal MAX_FUNDS_PER_DAY = 0;
        decimal tradedAmount = 0;

        Dictionary<int, decimal> stopLoss = new Dictionary<int, decimal>();

        // Unsettled funds
        Dictionary<DateTime, decimal> unclearedFunds = new Dictionary<DateTime, decimal>();

        // Target sell prices
        Dictionary<Symbol, decimal> sellTargets = new Dictionary<Symbol, decimal>();

        Crow.Watchlist highGains = null;
        Crow.Watchlist blacklist = null;

        public List<int> dayTrades;
        decimal dailyFunds = 0m;
        decimal buyingPower = 0m;
        int numTrades = 0;
        int maxTrades = 5;

        // Updates global 
        public void updateGlobals() {

            MAX_FUNDS_PER_DAY = Math.Floor(Portfolio.TotalPortfolioValue / 2);
            tradedAmount = 0;

            // Clear away stop losses
            stopLoss.Clear();
            BUYING_POWER = Portfolio.Cash;

            // Subtract unsettled funds from buying power
            if (LiveMode) unclearedFunds = ObjectStore.ReadJson<Dictionary<DateTime, decimal>>($"{ProjectId}/unclearedFunds");
            
            // Set funds for current date
            if (!unclearedFunds.ContainsKey(Time.Date)) {
                unclearedFunds[Time.Date] = 0;
            }

            // Loop through each date in uncleared funds
            foreach (DateTime time in unclearedFunds.Keys) {
                int clearTime = 2;
                if ((int)time.DayOfWeek == 6) clearTime = 4;
                // Check if we are past the clearing period
                if (Time.Date >= time.AddDays(clearTime) || time > Time.Date) {
                    unclearedFunds.Remove(time);
                } else if (BrokerageModel.AccountType == AccountType.Cash){
                    BUYING_POWER -= unclearedFunds[time];
                }
            }

            if (LiveMode) ObjectStore.SaveJson<Dictionary<DateTime, decimal>>($"{ProjectId}/unclearedFunds", unclearedFunds);

            // Reserve 20% of portfolio value per stock
            MAX_FUNDS_PER_STOCK = BUYING_POWER * 0.2m;
            MIN_FUNDS_PER_STOCK = MAX_FUNDS_PER_STOCK * 0.8m;
            RESERVED_FUNDS = 0m;

            if (MAX_FUNDS_PER_STOCK > MONEY_CAP) {
                MAX_FUNDS_PER_STOCK = MONEY_CAP;
                MIN_FUNDS_PER_STOCK = MONEY_CAP * 0.8m;
            }
        }

        // Initializes the algorithm
        public override void Initialize() {

            // Algorithm Settings
            SetStartDate(2023, 1, 1); ///< Backtest start date
            SetCash(6000); ///< Initial investment
            SetBrokerageModel(BrokerageName.TradierBrokerage, AccountType.Cash); ///< Trading Brokerage
            SetTimeZone(TimeZones.NewYork);
            SetWarmup(TimeSpan.FromMinutes(60));

            // Universe Settings
            UniverseSettings.ExtendedMarketHours = false; ///< Allow buying and selling pre-market and after market
            UniverseSettings.Resolution = Resolution.Minute; ///< Second Resolution Bars
            UniverseSettings.DataNormalizationMode = DataNormalizationMode.Raw; ///< Keep Raw price data

            // Add universe of assets
            AddUniverseSelection(new FineFundamentalUniverseSelectionModel(selectCoarse, selectFine));

            // Initialize Libraries
            Crow.Crow.init(this); ///< Crow library

            // Schedules
            Schedule.On(DateRules.EveryDay(), TimeRules.At(0, 0), Crow.AssetManager.reset); /// Reset asset data
            Schedule.On(DateRules.EveryDay(), TimeRules.At(0, 00), updateGlobals); /// Update global variables

            if (LiveMode) {
                Schedule.On(DateRules.EveryDay(), TimeRules.At(0, 00), forceRestart); /// Update global variables
            }

            // Watchlists
            highGains = new Crow.Watchlist(this); ///< Contains stocks to buy from
            blacklist = new Crow.Watchlist(this); ///< Contains list of stocks to stay away from
        }

        // Completes task after algorithm warmup
        public override void OnWarmupFinished() {
            base.OnWarmupFinished();
            updateGlobals();
            Debug(BUYING_POWER);
        }

        // Monitor Secuirty Changes
        public override void OnSecuritiesChanged(SecurityChanges changes) {

            // Loop through securities added to universe
            foreach(Security security in changes.AddedSecurities) {

                //security.SetSlippageModel(new VolumeShareSlippageModel());
                security.SetFillModel(new Crow.PennyStockFillModel(this));

                // Create Asset Object
                Crow.Asset asset = new Crow.Asset(this, security.Symbol);

                // Add window with 5 bars of data
                asset.addWindow(1);
                asset.addWindow(5);
                asset.addWindow(TimeSpan.FromMinutes(60));
                asset.addWindow(0);

                // Add a consolidator to the asset
                asset.addConsolidator(TimeSpan.FromMinutes(1), MinuteConsolidated);

                // Schedule sell off all holdings of asset 10 minutes before market close
                asset.addSchedule(Schedule.On(DateRules.EveryDay(), TimeRules.BeforeMarketClose(security.Symbol, 10), ()=> {

                    asset.isOpen = false;

                    // Update orders before market close
                    foreach (OrderTicket ticket in Transactions.GetOpenOrderTickets(security.Symbol)) {
                        Order order = Transactions.GetOrderById(ticket.OrderId);

                        // Cancel buy orders
                        if (order.Direction == OrderDirection.Buy) {
                            ticket.Cancel();
                        }

                        // Update sell orders
                        else if (Time > order.CreatedTime.AddDays(1)) {
                            ticket.Update(new UpdateOrderFields {LimitPrice = security.Close});
                        }
                    }
                }));
            }

            // Remove asset for each Security Removed from universe
            foreach (Security security in changes.RemovedSecurities) {

                // Stop watching Asset
                Crow.AssetManager.Remove(security.Symbol);
            }
        }

        // Forces the algorithm to restart
        public void forceRestart() {

            if (IsWarmingUp) return;

            // Cause error by trying to get non-existant item in array
            List<int> empty = new List<int>();
            int error = empty[0];

        }

        // Order events
        public override void OnOrderEvent(OrderEvent evnt) {

            // Update order events
            Crow.Crow.updateOrderEvents(evnt);

            // Get order ticket and ID from corresponding event
            Order order = Transactions.GetOrderById(evnt.OrderId);
            OrderTicket ticket = Transactions.GetOrderTicket(evnt.OrderId);

            // Check if this was a buy order
            if (evnt.Direction == OrderDirection.Buy) {

                // Record filled orders
                if (evnt.Status == OrderStatus.Filled) {
                    Debug($"[{Time}] Bought {ticket.QuantityFilled} shares of {evnt.Symbol.Value} for ${ticket.AverageFillPrice} each. Total: ${Math.Round(ticket.AverageFillPrice * ticket.QuantityFilled, 2)}");
                }

                // check if this is a limit order
                if (order.Type == OrderType.Limit) {
                    decimal limitPrice = ticket.Get(OrderField.LimitPrice);

                    // Add buying power back from ticket if ticket is cancelled or invalid
                    if (evnt.Status == OrderStatus.Canceled || evnt.Status == OrderStatus.Invalid) {
                        BUYING_POWER += (ticket.Quantity - ticket.QuantityFilled) * limitPrice;
                    }
            
                }

                // Check if order is a market order
                if (order.Type == OrderType.Market) {
                    if (evnt.FillQuantity != 0) BUYING_POWER -= evnt.FillQuantity * evnt.FillPrice;
                }

                // only place sell orders on securities we own
                if (!Portfolio[evnt.Symbol].Invested) return;

                // Place order to sell newly bought shares
                if (evnt.FillQuantity > 0) {

                    // Margin account doesn't need to wait for funds to clear
                    if (BrokerageModel.AccountType == AccountType.Margin) {
                        BUYING_POWER += evnt.FillQuantity * evnt.FillPrice;
                    }

                    tradedAmount += evnt.FillQuantity * evnt.FillPrice;

                    // Place a sell order for symbol
                    decimal targetPrice = Math.Round(ticket.AverageFillPrice * 1.05m, 2);
                    OrderTicket newOrder = LimitOrder(evnt.Symbol, evnt.FillQuantity * -1, Math.Round(targetPrice, 2));
                    stopLoss.Add(newOrder.OrderId, ticket.AverageFillPrice * 1.01m);
                }
            }

            // Check for sell order
            else if (evnt.Direction == OrderDirection.Sell) {

                // Record trading profit/loss
                if (evnt.FillQuantity != 0) {

                    // Add to uncleared funds
                    if (!unclearedFunds.ContainsKey(Time.Date)) unclearedFunds[Time.Date] = 0;
                    unclearedFunds[Time.Date] += Math.Abs(evnt.FillQuantity * evnt.FillPrice) ;
                    if (LiveMode) ObjectStore.SaveJson<Dictionary<DateTime, decimal>>($"{ProjectId}/unclearedFunds", unclearedFunds);

                    // Let us know how much we lost.
                    Debug($"[{Time}] Sold {Math.Abs(evnt.FillQuantity)} shares of {evnt.Symbol.Value} for ${ticket.AverageFillPrice} each. P/L: ${Portfolio[evnt.Symbol].LastTradeProfit}");
                }
            }
        }

        // Selects a universe of assets using coarse filter
        private IEnumerable<Symbol> selectCoarse(IEnumerable<CoarseFundamental> coarse) {

            // Select
            var selection = coarse.Where(c => c.Symbol.SecurityType == SecurityType.Equity)
                                  .Where(c => c.Price >= 3m)
                                  .Where(c => c.Price <= 10m)
                                  .Where(c => c.Symbol.Value != "WW")
                                  .Where(c => c.Symbol.SecurityType == SecurityType.Equity)
                                  .OrderByDescending(c => c.DollarVolume)
                                  .Select(c => c.Symbol);

            // Return list of selected symbols
            return selection;
        }

        private IEnumerable<Symbol> selectFine(IEnumerable<FineFundamental> fine) {

            var selection = fine.Where(f => f.MarketCap >= 10000).Select(f => f.Symbol);
            return selection;
        }


        // Action to take for every bar of data
        public override void OnData(Slice slice) {

            // Update assets with latest data
            Crow.AssetManager.onData(slice);
            
            foreach(TradeBar bar in slice.Bars.Values) {
                Crow.Asset asset = Crow.AssetManager.Get(bar.Symbol);
                if (Crow.AssetManager.assets.ContainsKey(bar.Symbol))asset.windows[2].update(bar);
            }

            if (IsWarmingUp) return;

            // Loop through data in slice
            foreach (KeyValuePair<Symbol, Split> kvp in slice.Splits) {

                // Add asset to blacklist;
                if (kvp.Value.Type == SplitType.Warning) {
                    blacklist.add(kvp.Key);

                    if (IsWarmingUp) continue;
                    Transactions.CancelOpenOrders(kvp.Key);
                    if (Portfolio[kvp.Key].Invested) MarketOrder(kvp.Key, Portfolio[kvp.Key].Quantity * -1);
                }

                // Remove asset from blacklist
                if (kvp.Value.Type == SplitType.SplitOccurred) {
                    blacklist.remove(kvp.Key);
                }
            }
        }

        // Consolidation function
        private void MinuteConsolidated(Object sender, TradeBar tradebar) {

            // Get stock assigned to symbol
            Crow.Asset asset = Crow.AssetManager.Get(tradebar.Symbol);

            // Update windows for this
            asset.windows[0].update(tradebar);
            asset.windows[1].update(tradebar);

            if (IsWarmingUp) return;
            if (!asset.isOpen) return;
            if (!Securities[asset.symbol].IsTradable) return;

            if (Portfolio[tradebar.Symbol].Invested && Transactions.GetOpenOrders(tradebar.Symbol).Count == 0) {
                LimitOrder(tradebar.Symbol, Portfolio[tradebar.Symbol].Quantity * -1, Math.Round(Portfolio[tradebar.Symbol].AveragePrice * 1.05m));
            }

            int swerve = Crow.Analyze.getSwerve(asset.windows[2].avgClose, asset.windows[2].history, 1);

            // Add asset with high gains to list
            if (swerve >= 6) {
                highGains.add(asset.symbol);
            }

            // Remove asset from watchlist if there is little movement
            else if (swerve <= 3) {
                highGains.remove(asset.symbol);
            }

            // Determine price to buy at
            decimal targetPrice = asset.windows[2].avgClose * 0.98m;
            if (asset.close < targetPrice) targetPrice = asset.close;

            bool hasOrder = false;
            foreach (Order order in Transactions.GetOpenOrders(asset.symbol)) {

                OrderTicket ticket = Transactions.GetOrderTicket(order.Id);
                decimal limit = ticket.Get(OrderField.LimitPrice);

                hasOrder = true;

                // Update order to take profits
                if (order.Direction == OrderDirection.Sell) {

                    if (!stopLoss.ContainsKey(order.Id)) stopLoss[order.Id] = limit;

                    if (asset.windows[2].avgClose > stopLoss[order.Id]) stopLoss[order.Id] = asset.windows[2].avgClose;

                    else if (asset.windows[2].avgClose < stopLoss[order.Id] - Portfolio[asset.symbol].AveragePrice * 0.03m) {
                        stopLoss[order.Id] -= Portfolio[asset.symbol].AveragePrice * 0.03m;
                        ticket.Update(new UpdateOrderFields() {LimitPrice = Math.Round(stopLoss[order.Id], 2)});
                    }

                    // Update sell orders if there is not enough swerve
                    if (order.Type == OrderType.Limit && swerve <= 3) {
                        if (limit != Math.Round(stopLoss[order.Id], 2)) ticket.Update(new UpdateOrderFields() {LimitPrice = Math.Round(stopLoss[order.Id], 2)});
                    }
                }

                else if (highGains.symbols.ContainsKey(asset.symbol)) {
                    if ((targetPrice) * ticket.Quantity > BUYING_POWER) {
                        ticket.Cancel();
                    } else {
                        if (targetPrice > limit) {
                            ticket.Update(new UpdateOrderFields() {LimitPrice = Math.Round(targetPrice, 2)});
                            BUYING_POWER += (limit * (ticket.Quantity - ticket.QuantityFilled));
                            BUYING_POWER -= (targetPrice * (ticket.Quantity - ticket.QuantityFilled));
                        }
                    }
                }

                else if (swerve <= 3) {
                    ticket.Cancel();
                }
            }

            // place buy order on asset
            if (highGains.symbols.ContainsKey(asset.symbol) && asset.close > asset.windows[2].avgClose * 1.03m) {
                if (!hasOrder) buy(asset.symbol, targetPrice);
            }
        }
        
        // Places buy order
        private void buy(Symbol symbol, decimal price, decimal maxQuantity=0) {

            // Don't buy anything in the blacklist
            if (blacklist.symbols.ContainsKey(symbol)) return;
            
            // Don't buy stock already owned
            if (Portfolio[symbol].Invested) return;

            // Don't buy shares if orders are placed.
            if (Transactions.GetOpenOrders(symbol).Count > 0) return;

            // Determine how much to allocate
            decimal budget = MIN_FUNDS_PER_STOCK;

            // Exit if we don't have enough funds availale
            if (BUYING_POWER < budget) return;

            // Determine Quantity to buy
            decimal quantity = Math.Floor(budget / price);

            // Ensure quantity is more than 0
            if (quantity <= 0) return;

            // Ensure there are enough shares to buy
            if (maxQuantity != 0 && quantity > maxQuantity) quantity = maxQuantity;

            // Subtract from buying power when order is placed
            if (BUYING_POWER - quantity * price <= 5) return;
            if (quantity * price > Portfolio.Cash) return;
            BUYING_POWER -= quantity * price;

            // Place an order on the asset
            int ticketID = LimitOrder(symbol, quantity, Math.Round(price, 2)).OrderId;
        }
    }
}